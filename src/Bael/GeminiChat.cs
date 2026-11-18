using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bael;

internal sealed record ChatTurn(string Role, string Text);

internal static class GeminiChat
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(25) };

    public static async Task<string?> ReplyAsync(BaelSettings settings, ChatMode mode, string message, IReadOnlyList<ChatTurn> history,
                                                  CancellationToken cancellationToken)
    {
        string instruction = Responses.Instruction(mode) + " Responde en español salvo las frases de práctica de inglés.";

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(settings.GeminiModel)}:generateContent");
        request.Headers.Add("x-goog-api-key", settings.EffectiveGeminiKey);
        request.Content = JsonContent.Create(new
        {
            system_instruction = new { parts = new[] { new { text = instruction } } },
            contents = history.Select(turn => new { role = turn.Role, parts = new[] { new { text = turn.Text } } })
                .Append(new { role = "user", parts = new[] { new { text = message } } }).ToArray()
        });

        using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        if (!json.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0
            || !candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
            return null;

        return string.Concat(parts.EnumerateArray().Select(part =>
            part.TryGetProperty("text", out var text) ? text.GetString() : null)).Trim();
    }
}
