using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Bael;

internal static class ChatRouterChecks
{
    internal static async Task RunAsync()
    {
        var modes = Enum.GetValues<ChatMode>();
        if (modes.Select(mode => ModePersonality.Default(mode).Tone).Distinct().Count() != modes.Length)
            throw new InvalidOperationException("Los modos perdieron su carácter predeterminado.");

        var custom = new ModePersonality
        {
            Tone = "Formal y respetuoso", Humor = 0, Address = "Yisus",
            Instructions = "Explícame con ejemplos de clase."
        };
        var personalized = new BaelSettings
        {
            Personalities = new Dictionary<ChatMode, ModePersonality> { [ChatMode.Math] = custom }
        };
        var saved = JsonSerializer.Deserialize<BaelSettings>(JsonSerializer.Serialize(personalized))!;
        if (saved.GetPersonality(ChatMode.Math) != custom
            || saved.GetPersonality(ChatMode.English) != ModePersonality.Default(ChatMode.English))
            throw new InvalidOperationException("No se conservaron las personalidades por modo.");
        foreach(var mode in modes)
        {
            const string answer="Una pista.\n[apuntes.pdf · página 3]";
            if((ModePersonality.Default(mode) with { Detail="Detallado",Address="Yisus",Humor=3 }).FormatLocal(mode,answer)!=answer)
                throw new InvalidOperationException("La guía local añadió relleno o quitó instrucciones y fuentes.");
        }
        if(Responses.For(ChatMode.Free,"hola")!="¡Hola! ¿Qué tal va tu día?" || Responses.For(ChatMode.Free,"gracias")!="¡De nada!")
            throw new InvalidOperationException("Los saludos breves perdieron su respuesta natural.");
        foreach (string provider in new[] { "Gemini", "Groq", "OpenRouter" })
        {
            using var request = ChatRouter.BuildRequest(new AiProfile
            {
                Provider = provider, Model = "test", Key = "fake"
            }, ChatMode.Math, "mi intento", [], custom);
            string body = await request.Content!.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);
            string prompt = provider == "Gemini"
                ? json.RootElement.GetProperty("system_instruction").GetProperty("parts")[0].GetProperty("text").GetString()!
                : json.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
            if (!prompt.Contains("Formal y respetuoso") || !prompt.Contains("UNA pista") || !prompt.Contains("ejemplos de clase") || !prompt.Contains("dos a cuatro frases") || !prompt.Contains("voz como escrito"))
                throw new InvalidOperationException("Una API perdió el carácter o la función del tutor.");
        }

        var handler = new FailoverHandler();
        using var client = new HttpClient(handler);
        var router = new ChatRouter(client);
        var settings = new BaelSettings
        {
            OnlineChat = true,
            Profiles =
            [
                new() { Provider = "Gemini", Model = "test", Key = "fake-gemini" },
                new() { Provider = "Groq", Model = "test", Key = "fake-groq" }
            ]
        };
        var history = new[] { new ChatTurn("user", "Mi intento"), new ChatTurn("model", "Haz el primer paso.") };
        ChatResult result = await router.ReplyAsync(settings, ChatMode.Math, "¿Y ahora?", history, CancellationToken.None);
        if (result.Text != "Una pista" || handler.GeminiCalls != 1 || handler.GroqCalls != 1)
            throw new InvalidOperationException("El relevo tras la cuota agotada falló.");

        await router.ReplyAsync(settings, ChatMode.Math, "Continúa", history, CancellationToken.None);
        if (handler.GeminiCalls != 1 || handler.GroqCalls != 2)
            throw new InvalidOperationException("Se volvió a llamar al proveedor en espera.");

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        try
        {
            await router.ReplyAsync(settings, ChatMode.Math, "Cancelar", history, canceled.Token);
            throw new InvalidOperationException("La cancelación no detuvo el relevo.");
        }
        catch (OperationCanceledException)
        {
            if (handler.GroqCalls != 2)
                throw new InvalidOperationException("Se enviaron datos después de cancelar.");
        }

        var local = await new ChatRouter(client).ReplyAsync(settings with { AutoFallback = false },
            ChatMode.Math, "Ejercicio", history, CancellationToken.None);
        if (local.Text is not null || handler.GroqCalls != 2)
            throw new InvalidOperationException("Se ignoró el relevo desactivado.");

        var legacy = new BaelSettings { GeminiKey = "legacy", GeminiModel = "legacy-model" };
        BaelSettings restored = JsonSerializer.Deserialize<BaelSettings>(JsonSerializer.Serialize(legacy))!;
        if (restored.GetProfiles()[0].Key != "legacy" || restored.GetProfiles()[0].Model != "legacy-model")
            throw new InvalidOperationException("La configuración anterior no se conservó.");

        var allFailed = await new ChatRouter(client).ReplyAsync(settings with
        {
            Profiles = [new() { Provider = "OpenRouter", Model = "openrouter/free", Key = "fake" }]
        }, ChatMode.Math, "Prueba", history, CancellationToken.None);
        if (allFailed.Text is not null)
            throw new InvalidOperationException("Una respuesta vacía se trató como válida.");
    }

    private sealed class FailoverHandler : HttpMessageHandler
    {
        public int GeminiCalls { get; private set; }
        public int GroqCalls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "generativelanguage.googleapis.com")
            {
                GeminiCalls++;
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            }

            if (request.RequestUri.Host == "api.groq.com")
            {
                GroqCalls++;
                using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var messages = payload.RootElement.GetProperty("messages");
                if (messages[2].GetProperty("role").GetString() != "assistant"
                    || !messages[0].GetProperty("content").GetString()!.Contains("UNA pista"))
                    throw new InvalidOperationException("El relevo perdió el contexto o la instrucción del tutor.");

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Una pista\"}}]}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[]}", Encoding.UTF8, "application/json")
            };
        }
    }
}
