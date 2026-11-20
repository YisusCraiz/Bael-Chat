using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bael;

internal sealed record VoiceChoice(string Id, string Name)
{
    public string Label => $"{Name} · {Id}";
}

internal static class ElevenLabsVoice
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
    public static readonly string[] Models = ["eleven_multilingual_v2", "eleven_flash_v2_5", "eleven_v3"];

    public static async Task<byte[]> GenerateAsync(BaelSettings settings, string text,
        CancellationToken cancellationToken, HttpClient? client = null)
    {
        string key = settings.EffectiveVoiceKey;
        string voiceId = settings.EffectiveVoiceId.Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(voiceId))
            throw new InvalidOperationException("Configura la clave y el ID de voz de ElevenLabs desde Ajustes.");
        if (voiceId.Contains('/') || voiceId.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("En ID de voz pega solo el identificador, o elige una voz con Cargar voces. Una URL o un nombre de voz no sirven como ID.");

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(voiceId)}?output_format=mp3_44100_128");
        request.Headers.Add("xi-api-key", key);
        request.Content = JsonContent.Create(new { text, model_id = settings.VoiceModel.Trim() });

        using HttpResponseMessage response = await (client ?? Client).SendAsync(request, cancellationToken);
        await CheckResponse(response, key, cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public static async Task<VoiceChoice[]> GetVoicesAsync(string key, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Agrega la clave de ElevenLabs para cargar tus voces.");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.elevenlabs.io/v2/voices?page_size=100");
        request.Headers.Add("xi-api-key", key);
        using var response = await Client.SendAsync(request, token);
        await CheckResponse(response, key, token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        return json.RootElement.GetProperty("voices").EnumerateArray()
            .Select(voice => new VoiceChoice(voice.GetProperty("voice_id").GetString()!,
                voice.GetProperty("name").GetString()!)).OrderBy(voice => voice.Name).ToArray();
    }

    private static async Task CheckResponse(HttpResponseMessage response, string key, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        string body = await response.Content.ReadAsStringAsync(token);
        throw new InvalidOperationException(DescribeError((int)response.StatusCode, body, key));
    }

    internal static string DescribeError(int code, string body, string key)
    {
        string status = "";
        string message = "";
        try
        {
            using var json = JsonDocument.Parse(body);
            JsonElement detail = json.RootElement.TryGetProperty("detail", out var found) ? found : json.RootElement;
            if (detail.ValueKind == JsonValueKind.Object)
            {
                if (detail.TryGetProperty("status", out var state)) status = state.ToString();
                if (detail.TryGetProperty("message", out var text)) message = text.ToString();
            }
            else if (detail.ValueKind == JsonValueKind.String)
                message = detail.GetString() ?? "";
        }
        catch (JsonException) { }

        string description = status switch
        {
            "voice_not_found" or "invalid_voice_id" => "ElevenLabs no encontró ese ID de voz. Usa Cargar voces y selecciona una de tu cuenta.",
            "invalid_api_key" => "ElevenLabs rechazó la clave. Comprueba el acceso a Text to Speech.",
            "quota_exceeded" or "insufficient_credits" => "ElevenLabs indica que no hay cuota suficiente para esta lectura.",
            "max_character_limit_exceeded" or "text_too_long" => "La respuesta supera el tamaño permitido por el modelo de voz.",
            _ when message.Contains("free", StringComparison.OrdinalIgnoreCase)
                || status.Contains("paid", StringComparison.OrdinalIgnoreCase)
                => "ElevenLabs indica una restricción del plan para esa voz. Elige una voz permitida en tu cuenta.",
            _ when status.Contains("model", StringComparison.OrdinalIgnoreCase)
                => "Revisa el modelo de lectura en Ajustes → Voz y audio y su compatibilidad con esa voz.",
            _ => "ElevenLabs rechazó la solicitud de voz. Revisa el detalle del servicio."
        };
        if (!string.IsNullOrEmpty(key))
        {
            message = message.Replace(key, "[clave protegida]");
            status = status.Replace(key, "[clave protegida]");
        }
        if (message.Length > 500) message = message[..500] + "…";
        return $"ElevenLabs ({code}{(status.Length == 0 ? "" : " · " + status)}): {description}" +
            (message.Length == 0 ? "" : "\nDetalle: " + message);
    }

    internal static async Task VerifyAsync()
    {
        string detail = DescribeError(400, "{\"detail\":{\"status\":\"voice_not_found\",\"message\":\"Voice not found\"}}", "secret");
        if (!detail.Contains("voice_not_found") || !detail.Contains("Cargar voces"))
            throw new InvalidOperationException("Se perdió el detalle del rechazo de voz.");
        string protectedText = DescribeError(401, "{\"detail\":\"secret\"}", "secret");
        if (protectedText.Contains("secret"))
            throw new InvalidOperationException("El mensaje expuso una clave.");
        using var client = new HttpClient(new VoiceErrorHandler());
        try
        {
            await GenerateAsync(new BaelSettings { VoiceKey = "fake", VoiceId = "voice-test",
                VoiceModel = "eleven_flash_v2_5" }, "Prueba", CancellationToken.None, client);
            throw new InvalidOperationException("El rechazo de voz no se notificó.");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("voice_not_found")) { }
    }

    private sealed class VoiceErrorHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (!body.Contains("eleven_flash_v2_5"))
                throw new InvalidOperationException("No se aplicó el modelo de voz seleccionado.");
            return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"detail\":{\"status\":\"voice_not_found\",\"message\":\"Voice not found\"}}")
            };
        }
    }
}
