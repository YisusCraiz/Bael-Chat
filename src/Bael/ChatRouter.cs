using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bael;

internal sealed record ChatResult(string? Text, string Status);

internal sealed class ChatRouter
{
    private readonly HttpClient client;
    private readonly Dictionary<string, DateTimeOffset> unavailableUntil = new();

    public ChatRouter(HttpClient? client = null)
    {
        this.client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
    }

    public async Task<ChatResult> ReplyAsync(BaelSettings settings, ChatMode mode, string message,
        IReadOnlyList<ChatTurn> history, CancellationToken cancellationToken, IProgress<string>? progress = null)
    {
        AiProfile[] profiles = settings.GetProfiles().Where(profile => profile.Enabled &&
            AiProviders.KeyFor(profile).Length > 0).ToArray();
        if (!settings.AutoFallback)
            profiles = profiles.Take(1).ToArray();

        var failures = new List<string>();
        foreach (AiProfile profile in profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                profile.Provider + "\n" + profile.Model + "\n" + AiProviders.KeyFor(profile))));
            if (unavailableUntil.TryGetValue(identity, out var until) && until > DateTimeOffset.UtcNow)
            {
                failures.Add($"{profile.Provider} en espera");
                continue;
            }

            try
            {
                progress?.Report($"Consultando {profile.Label}…");
                using HttpRequestMessage request = BuildRequest(profile, mode, message, history, settings.GetPersonality(mode));
                using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    int code = (int)response.StatusCode;
                    string reason = code switch
                    {
                        429 or 402 => "cuota agotada",
                        401 or 403 => "revisa clave o permisos",
                        400 or 404 => "revisa modelo o solicitud",
                        _ => "servicio no disponible"
                    };
                    TimeSpan delay = code is 429 or 402 or 401 or 403 or 400 or 404
                        ? TimeSpan.FromMinutes(15) : TimeSpan.FromSeconds(30);
                    if (response.Headers.RetryAfter?.Delta is TimeSpan retry)
                        delay = retry > delay ? retry : delay;
                    else if (response.Headers.RetryAfter?.Date is DateTimeOffset date && date > DateTimeOffset.UtcNow + delay)
                        delay = date - DateTimeOffset.UtcNow;
                    unavailableUntil[identity] = DateTimeOffset.UtcNow + delay;
                    failures.Add($"{profile.Provider}: {reason} ({code})");
                    continue;
                }

                using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken));
                string? text = ReadText(profile.Provider, json.RootElement);
                if (string.IsNullOrWhiteSpace(text))
                {
                    failures.Add($"{profile.Provider}: respuesta vacía o bloqueada");
                    unavailableUntil[identity] = DateTimeOffset.UtcNow.AddSeconds(30);
                    continue;
                }

                unavailableUntil.Remove(identity);
                string source = $"Respondió {profile.Label}";
                return new(text, failures.Count == 0 ? source : $"{string.Join(" · ", failures)}. {source}");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                unavailableUntil[identity] = DateTimeOffset.UtcNow.AddSeconds(30);
                failures.Add($"{profile.Provider}: tiempo agotado");
            }
            catch (Exception error) when (error is HttpRequestException or JsonException or FormatException or InvalidOperationException)
            {
                unavailableUntil[identity] = DateTimeOffset.UtcNow.AddSeconds(30);
                failures.Add($"{profile.Provider}: conexión o respuesta no válida");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new(null, failures.Count == 0
            ? "Agrega y activa una API con clave en Ajustes. Se mostró una guía local."
            : $"{string.Join(" · ", failures)}. Se mostró una guía local.");
    }

    internal static HttpRequestMessage BuildRequest(AiProfile profile, ChatMode mode, string message,
        IReadOnlyList<ChatTurn> history, ModePersonality? personality = null)
    {
        string instruction = Responses.Instruction(mode) + " " + (personality ?? ModePersonality.Default(mode)).Prompt() + " Responde en español salvo las frases de práctica de inglés. Los cambios de modo los ejecuta la aplicación; si no recibes una confirmación real, no afirmes haber cambiado de modo. Indica que puede decir cambia al modo seguido del nombre. No inventes funciones de la aplicación.";
        if (profile.Provider == "Gemini")
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(profile.Model)}:generateContent");
            request.Headers.Add("x-goog-api-key", AiProviders.KeyFor(profile));
            request.Content = JsonContent.Create(new
            {
                system_instruction = new { parts = new[] { new { text = instruction } } },
                contents = history.Select(turn => new { role = turn.Role, parts = new[] { new { text = turn.Text } } })
                    .Append(new { role = "user", parts = new[] { new { text = message } } }).ToArray()
            });
            return request;
        }

        string endpoint = profile.Provider == "Groq"
            ? "https://api.groq.com/openai/v1/chat/completions"
            : "https://openrouter.ai/api/v1/chat/completions";
        var compatible = new HttpRequestMessage(HttpMethod.Post, endpoint);
        compatible.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AiProviders.KeyFor(profile));
        compatible.Content = JsonContent.Create(new
        {
            model = profile.Model,
            messages = new[] { new { role = "system", content = instruction } }
                .Concat(history.Select(turn => new { role = turn.Role == "model" ? "assistant" : "user", content = turn.Text }))
                .Append(new { role = "user", content = message }).ToArray()
        });
        return compatible;
    }

    private static string? ReadText(string provider, JsonElement root)
    {
        if (provider == "Gemini")
        {
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0
                || !candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
                return null;
            return string.Concat(parts.EnumerateArray()
                .Where(part => !part.TryGetProperty("thought", out var thought) || thought.ValueKind != JsonValueKind.True)
                .Select(part => part.TryGetProperty("text", out var text) ? text.GetString() : null)).Trim();
        }

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out var reply) || !reply.TryGetProperty("content", out var result))
            return null;
        return result.ValueKind == JsonValueKind.String ? result.GetString() : null;
    }
}
