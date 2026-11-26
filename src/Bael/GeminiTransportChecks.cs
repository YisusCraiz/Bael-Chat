using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Bael;

internal static class GeminiTransportChecks
{
    private static string Event(string text,bool finish=false,bool thought=false)=>"data: "+JsonSerializer.Serialize(new
    {
        candidates=new[] { new { content=new { parts=new[]{new { text,thought }} },finishReason=finish?"STOP":null } }
    })+"\n\n";
    internal static async Task RunAsync()
    {
        using(var content=new StringContent(": keep-alive\n\n"+Event("invisible",thought:true)+Event("¡Hola ")+Event("Yisus!",finish:true),Encoding.UTF8,"text/event-stream"))
        {
            var counts=new List<int>();
            string answer=await ChatRouter.ReadGeminiStream(content,CancellationToken.None,counts.Add);
            if(answer!="¡Hola Yisus!" || !counts.Contains(0) || counts[^1]!=answer.Length) throw new InvalidOperationException("El flujo perdió espacios, texto o mostró pensamientos internos.");
        }
        using(var incomplete=new StringContent(Event("Respuesta a medias"),Encoding.UTF8,"text/event-stream"))
        {
            try { await ChatRouter.ReadGeminiStream(incomplete,CancellationToken.None);throw new Exception("Se aceptó una respuesta interrumpida como completa."); }
            catch(InvalidOperationException) { }
        }
        var probeHandler=new ProbeHandler();using var probeClient=new HttpClient(probeHandler);
        var result=await new ChatRouter(probeClient).DiagnoseAsync(new AiProfile { Provider="Gemini",Model="gemini-3.8-flash",Key="fake" },CancellationToken.None);
        if(result.Text!="OK" || probeHandler.Calls!=2 || !result.Status.Contains("HTTP 200")) throw new InvalidOperationException("El diagnóstico no comprobó modelo y generación mínima.");
        var progress=new InlineProgress();using var stalledClient=new HttpClient(new StalledHandler());
        using var cancel=new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try
        {
            await new ChatRouter(stalledClient).ReplyAsync(new BaelSettings { Profiles=[new(){Provider="Gemini",Model="gemini-3.8-flash",Key="fake"}],AutoFallback=false },ChatMode.Free,"hola",[],cancel.Token,progress);
            throw new Exception("La cancelación no interrumpió la lectura de un flujo detenido.");
        }
        catch(OperationCanceledException) when(cancel.IsCancellationRequested)
        {
            if(!progress.SawHeaders) throw new InvalidOperationException("Se esperó a todo el cuerpo antes de mostrar las cabeceras HTTP.");
        }
    }
    private sealed class InlineProgress : IProgress<string>
    {
        internal bool SawHeaders;
        public void Report(string value) { if(value.Contains("HTTP 200")) SawHeaders=true; }
    }
    private sealed class ProbeHandler : HttpMessageHandler
    {
        internal int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Calls++;
            if(request.Method==HttpMethod.Get) return new(HttpStatusCode.OK) { Content=new StringContent("{}",Encoding.UTF8,"application/json") };
            if(!request.RequestUri!.Query.Contains("alt=sse") || !request.RequestUri.AbsolutePath.EndsWith(":streamGenerateContent")) throw new InvalidOperationException("Gemini no solicitó SSE.");
            using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var root=json.RootElement;
            if(root.GetProperty("contents").GetArrayLength()!=1 || root.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!="OK" || !root.GetProperty("system_instruction").GetProperty("parts")[0].GetProperty("text").GetString()!.Contains("prueba técnica")) throw new InvalidOperationException("La prueba mínima incluyó un contexto que no corresponde.");
            return new(HttpStatusCode.OK) { Content=new StringContent(Event("OK",finish:true),Encoding.UTF8,"text/event-stream") };
        }
    }
    private sealed class StalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var content=new StreamContent(new StalledStream());content.Headers.ContentType=new("text/event-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=content });
        }
    }
    private sealed class StalledStream : Stream
    {
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position {get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
        { await Task.Delay(Timeout.Infinite,token);return 0; }
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
