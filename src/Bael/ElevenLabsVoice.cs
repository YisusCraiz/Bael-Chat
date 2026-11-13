using System.Net.Http;
using System.Net.Http.Json;

namespace Bael;

internal static class ElevenLabsVoice
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static async Task<byte[]> GenerateAsync(BaelSettings settings, string text, CancellationToken cancellationToken)
    {
        string key = settings.EffectiveVoiceKey;
        string voiceId = settings.EffectiveVoiceId;
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(voiceId))
            throw new InvalidOperationException("Configura la clave y el ID de voz de ElevenLabs desde Ajustes.");

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(voiceId)}?output_format=mp3_44100_128");
        request.Headers.Add("xi-api-key", key);
        request.Content = JsonContent.Create(new { text, model_id = "eleven_multilingual_v2" });

        using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
