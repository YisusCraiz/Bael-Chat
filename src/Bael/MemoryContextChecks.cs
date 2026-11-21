using System.Net.Http;

namespace Bael;

internal static class MemoryContextChecks
{
    internal static async Task RunAsync()
    {
        var data=new MemoryData(); data.Notes.Add(new("one",ChatMode.Free,"MEMORIA_ELEGIDA_UNICA"));
        data.Notes.Add(new("two",ChatMode.English,"OTRO_MODO_UNICO"));
        var settings=new BaelSettings { OnlineChat=true, Profiles=[new() { Provider="Groq",Model="test",Key="fake" }] };
        using var handler=new InspectHandler(); using var client=new HttpClient(handler); var router=new ChatRouter(client);
        ChatTurn[] history=[new("user","CONTEXTO_PREVIO_UNICO"),new("model","Respuesta anterior")];
        await router.ReplyAsync(settings,ChatMode.Free,"Hola",history,CancellationToken.None,memoryContext:data.ContextFor(ChatMode.Free));
        if(!handler.Body.Contains("MEMORIA_ELEGIDA_UNICA") || !handler.Body.Contains("CONTEXTO_PREVIO_UNICO") || handler.Body.Contains("OTRO_MODO_UNICO"))
            throw new InvalidOperationException("Se perdió el contexto permitido o se mezclaron recuerdos.");
        await router.ReplyAsync(settings with { UseConversationContext=false,UsePinnedMemories=false },ChatMode.Free,"Hola",history,CancellationToken.None,memoryContext:data.ContextFor(ChatMode.Free));
        if(handler.Body.Contains("MEMORIA_ELEGIDA_UNICA") || handler.Body.Contains("CONTEXTO_PREVIO_UNICO"))
            throw new InvalidOperationException("Se ignoraron los controles de memoria.");
        await router.ReplyAsync(settings with { DocumentRules=new Dictionary<ChatMode,DocumentRule> { [ChatMode.Free]=new() { Limited=true } } },
            ChatMode.Free,"Hola",history,CancellationToken.None,memoryContext:data.ContextFor(ChatMode.Free));
        if(handler.Body.Contains("MEMORIA_ELEGIDA_UNICA") || handler.Body.Contains("CONTEXTO_PREVIO_UNICO"))
            throw new InvalidOperationException("Los recuerdos saltaron el límite PDF.");
    }
    private sealed class InspectHandler : HttpMessageHandler
    {
        internal string Body { get; private set; }="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Body=await request.Content!.ReadAsStringAsync(token);
            return new(System.Net.HttpStatusCode.OK) { Content=new StringContent("{\"choices\":[{\"message\":{\"content\":\"Respuesta\"}}]}") };
        }
    }
}
