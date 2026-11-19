namespace Bael;

internal sealed record ModePersonality
{
    public string Tone { get; init; } = "Cercano y juguetón";
    public int Humor { get; init; } = 1;
    public string Detail { get; init; } = "Equilibrado";
    public string Address { get; init; } = "";
    public string Instructions { get; init; } = "";

    public static readonly string[] Tones =
    [
        "Cercano y juguetón", "Misterioso y teatral", "Ingenioso y bromista",
        "Sereno y directo", "Paciente y motivador", "Alegre y expresivo", "Formal y respetuoso"
    ];
    public static readonly string[] Details = ["Breve", "Equilibrado", "Detallado"];

    public static ModePersonality Default(ChatMode mode) => mode switch
    {
        ChatMode.Student => new() { Tone = Tones[0], Humor = 2 },
        ChatMode.Tarot => new() { Tone = Tones[1], Humor = 1 },
        ChatMode.Comedy => new() { Tone = Tones[2], Humor = 3 },
        ChatMode.System => new() { Tone = Tones[3], Humor = 0, Detail = "Breve" },
        ChatMode.Math => new() { Tone = Tones[4], Humor = 1 },
        ChatMode.English => new() { Tone = Tones[5], Humor = 2 },
        _ => new()
    };

    public string Prompt() =>
        $"Carácter actual: {Tone}. Humor: {Math.Clamp(Humor, 0, 3)} de 3, donde 0 evita bromas. " +
        $"Extensión preferida: {Detail}. " +
        (string.IsNullOrWhiteSpace(Address) ? "" : $"Dirígete al usuario como «{Address}» cuando encaje. ") +
        $"Preferencias de expresión del usuario: «{Instructions}». " +
        "Aplica estas preferencias al tono y la forma, conservando el propósito del modo y las reglas de enseñanza.";

    public string FormatLocal(ChatMode mode, string reply)
    {
        string opening = Tone switch
        {
            "Cercano y juguetón" => "Vamos a ello.",
            "Misterioso y teatral" => "Abramos esta pequeña escena.",
            "Ingenioso y bromista" => "Tengo una idea.",
            "Paciente y motivador" => "Vamos paso a paso.",
            "Alegre y expresivo" => "¡Vamos a practicar!",
            "Formal y respetuoso" => "Con gusto.",
            _ => "Revisemos."
        };
        string address = string.IsNullOrWhiteSpace(Address) ? "" : $"{Address}, ";
        string joke = Humor >= 2 ? mode switch
        {
            ChatMode.Student => " El unicornio también está en su arco de estudio.",
            ChatMode.Tarot => " Las cartas prometieron llegar antes que la tarea.",
            ChatMode.Comedy => " Al menos la mochila ya tiene experiencia cargando problemas.",
            ChatMode.System => " Hoy el reinicio no se llevará todo el crédito.",
            ChatMode.Math => " La incógnita puede esconderse, pero vamos a encontrar el camino.",
            _ => " Hasta los verbos necesitan ensayar su papel."
        } : "";
        string extra = Detail == "Detallado" ? "\nCuéntame qué has intentado para adaptar el siguiente paso." : "";
        return $"{address}{opening}\n{reply}{joke}{extra}";
    }
}
