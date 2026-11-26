namespace Bael;

internal sealed record AiProfile
{
    public string Provider { get; init; } = "Gemini";
    public string Model { get; init; } = "gemini-3.8-flash";
    public string Key { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public string Label => $"{Provider} · {Model}{(Enabled ? "" : " · desactivado")}";
}

internal sealed record ProviderInfo(string Name, string Model, string Description, string KeyUrl, string DocsUrl);

internal static class AiProviders
{
    public static readonly ProviderInfo[] All =
    [
        new("Gemini", "gemini-3.8-flash",
            "Gemini 3.8 Flash es la opción principal. Gemini 2.5 Flash ya no está disponible para cuentas nuevas. Puedes elegir 3.5 Flash si tu cuenta tiene acceso o escribir otro ID. La disponibilidad, precio y cuota dependen de tu cuenta; compruébalos en Google AI Studio. Probar conexión muestra el detalle del servicio.",
            "https://aistudio.google.com/apikey", "https://ai.google.dev/gemini-api/docs/pricing"),
        new("Groq", "openai/gpt-oss-120b",
            "Conversación y razonamiento con GPT-OSS 120B. Groq ofrece un plan gratuito con límites de solicitudes y tokens. La cuota exacta aparece en tu cuenta.",
            "https://console.groq.com/keys", "https://console.groq.com/docs/rate-limits"),
        new("OpenRouter", "openrouter/free",
            "Respaldo que elige un modelo gratuito disponible. La calidad y disponibilidad pueden variar. Usa openrouter/free o un ID terminado en :free; otros modelos pueden tener costo.",
            "https://openrouter.ai/settings/keys", "https://openrouter.ai/docs/guides/routing/routers/free-router")
    ];

    public static readonly string[] GeminiModels=["gemini-3.8-flash","gemini-3.5-flash"];
    public static string NormalizeModel(string provider,string model)
    {
        model=model.Trim();
        if(provider!="Gemini") return model;
        if(model.StartsWith("models/",StringComparison.OrdinalIgnoreCase)) model=model[7..];
        return model.ToLowerInvariant().Replace("gémini","gemini");
    }
    public static ProviderInfo Get(string name) => All.First(info => info.Name == name);
    public static string KeyFor(AiProfile profile) => !string.IsNullOrWhiteSpace(profile.Key)
        ? profile.Key.Trim() : Environment.GetEnvironmentVariable(profile.Provider switch
        {
            "Groq" => "BAEL_GROQ_API_KEY",
            "OpenRouter" => "BAEL_OPENROUTER_API_KEY",
            _ => "BAEL_GEMINI_API_KEY"
        })?.Trim() ?? "";
}
