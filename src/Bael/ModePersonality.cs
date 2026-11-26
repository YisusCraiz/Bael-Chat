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
        "Sereno y directo", "Paciente y motivador", "Alegre y expresivo", "Formal y respetuoso",
        "Cómplice y curioso", "Competitivo y teatral"
    ];
    public static readonly string[] Details = ["Breve", "Equilibrado", "Detallado"];

    public static ModePersonality Default(ChatMode mode) => mode switch
    {
        ChatMode.Free => new() { Tone = Tones[7], Humor = 2 },
        ChatMode.Gamer => new() { Tone = Tones[8], Humor = 3 },
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
        "Aplica estas preferencias al tono y la forma, conservando el propósito del modo y las reglas de enseñanza. " +
        "Habla de forma natural, como en una conversación cotidiana, tanto si el mensaje viene por voz como escrito. " +
        "Responde primero a lo que acaba de decir el usuario y sigue el hilo; no vuelvas a presentarte ni expliques el modo en cada turno. " +
        "Evita saludos de plantilla, muletillas repetidas, moralejas, resúmenes y bromas añadidas sin relación. " +
        "Usa el nombre solo cuando encaje, no en cada respuesta. Haz como máximo una pregunta y solo si ayuda a continuar. " +
        "No desvíes la charla ni ofrezcas actividades nuevas si no vienen al caso. No fuerces listas ni encabezados en la charla normal. " +
        (Detail=="Breve"?"Por defecto usa una o dos frases cortas. ":Detail=="Detallado"?"Puedes dar más detalle útil, sin repetir ideas ni alargarte con introducciones. ":"Por defecto usa dos a cuatro frases cortas, alrededor de 25 a 60 palabras; si basta menos, di menos. ") +
        "Amplía si el usuario lo pide o si hace falta para explicar bien el punto. Estas pautas de brevedad no sustituyen los formatos, las fuentes ni los pasos necesarios de las actividades.";

    // Local activities already carry their own voice and teaching steps.
    // Never decorate every turn or trim away instructions and source references.
    public string FormatLocal(ChatMode mode,string reply) => reply.Trim();
}
