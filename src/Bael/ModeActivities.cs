namespace Bael;

internal sealed record ModeActivity(string Label, string Prompt);

internal sealed class ModeActivities
{
    private readonly Dictionary<ChatMode, int> progress = new();
    internal bool IsRunning(ChatMode mode) => progress.ContainsKey(mode);
    internal void Stop(ChatMode mode) { progress.Remove(mode); scores.Remove(mode); }
    private readonly Dictionary<ChatMode, int> scores = new();
    private static readonly string[] MathQuestions =
    [
        "Reto 1/3: en 2x + 4 = 10, ¿qué número restarías a ambos lados? Escribe solo ese número.",
        "Reto 2/3: ahora tienes 2x = 6. ¿Entre qué número dividirías ambos lados? Escribe el divisor, sin calcular todavía x.",
        "Reto 3/3: en 3y − 5 = 7, ¿qué número sumarías a ambos lados? Tú eliges el siguiente paso."
    ];
    private static readonly string[] EnglishQuestions =
    [
        "Quiz 1/4 · Presente: She ___ English every day. (study / studies)",
        "Quiz 2/4 · Pasado: Yesterday I ___ to school. (go / went)",
        "Quiz 3/4 · Futuro: Tomorrow she will ___ a book. (read / reads)",
        "Quiz 4/4 · Preguntas: ___ you like music? (Do / Does)"
    ];
    private static readonly string[] EnglishAnswers = ["studies", "went", "read", "do"];
    private static readonly string[] Explanations =
    ["Con she usamos studies en presente.", "Go tiene pasado irregular: went.",
     "Después de will va el verbo base: read.", "Con you usamos Do para preguntar en presente."];
    private static readonly string[] Jokes =
    [
        "Mi plan de estudio es como el Wi-Fi del campus: existe, pero a veces no conecta.",
        "Le pedí espacio a mi computadora y me abrió otra pestaña. Comunicación impecable.",
        "Mi código funcionó a la primera. Luego desperté.",
        "El álgebra me pidió encontrar x. Le dije que primero revisara objetos perdidos.",
        "El unicornio entregó la tarea a tiempo. Era un trabajo de fantasía."
    ];
    private static readonly (string Name, string Question)[] Cards =
    [
        ("El Ermitaño", "¿Qué podrías entender mejor si te das una pausa?"),
        ("La Estrella", "¿Qué pequeño proyecto te devuelve la ilusión?"),
        ("La Fuerza", "¿En qué situación podrías responder con paciencia?"),
        ("El Mago", "¿Qué recurso que ya tienes podrías aprovechar hoy?"),
        ("El Carro", "¿Cuál sería un primer paso que dependa de ti?"),
        ("La Templanza", "¿Qué dos responsabilidades necesitas equilibrar?")
    ];

    internal static string Description(ChatMode mode) => mode switch
    {
        ChatMode.Student => "Tu compañero de estudio · organiza, repasa y consulta tus PDF. Se respeta el límite de documentos.",
        ChatMode.Math => "Entrenamiento con pistas · tú haces los cálculos. El reto guiado funciona sin conexión.",
        ChatMode.English => "Aula por niveles A1–B2 · escritura, lectura, escucha y conversación con progreso local.",
        ChatMode.System => "Tu asistente técnico · Centro del sistema con recursos, discos, RAM, archivos y temporales.",
        ChatMode.Tarot => "Una pausa teatral · cartas imaginarias para reflexionar, sin predicciones.",
        ChatMode.Comedy => "Recreo del campus · chistes, improvisación y pequeñas historias.",
        ChatMode.Free => "Hablemos a nuestro ritmo · tu día, gustos, ideas o una aventura de decisiones.",
        ChatMode.Gamer => "La sala de BAEL · vasos y póker con fichas ficticias. Espectáculo anunciado: BAEL siempre gana.",
        _ => ""
    };

    internal static ModeActivity[] Catalog(ChatMode mode) => mode switch
    {
        ChatMode.Student => [new("Plan de estudio", "Ayúdame a hacer un plan de estudio por bloques de 20 minutos."),
            new("Tarjetas", "Crea tarjetas de repaso del tema que estamos estudiando; pregúntame una a la vez."),
            new("Repaso oral", "Hazme una pregunta de repaso del material y espera mi intento."),
            new("Explicar fácil", "Explícame el concepto principal del tema con un ejemplo cotidiano.")],
        ChatMode.Math => [new("Reto guiado", "Reto guiado"), new("Fracciones", "Ayúdame con fracciones usando una pista por turno."),
            new("Álgebra", "Practiquemos despejar una ecuación sin darme el resultado."), new("Revisar intento", "Quiero revisar mi procedimiento; pídeme el ejercicio y mi primer paso.")],
        ChatMode.English => [new("Aula por niveles", "@english:0"), new("Escritura y corrección", "@english:1"),
            new("Escucha de 50 palabras", "@english:3"), new("Conversaciones", "@english:4")],
        ChatMode.System => [new("Centro del sistema", "@system"), new("Buscar archivos", "@files"),
            new("Liberar espacio", "@cleanup"), new("RAM y ampliación", "@ram")],
        ChatMode.Tarot => [new("Tres cartas", "Tirada de 3 cartas"), new("Carta del día", "Carta del día"),
            new("Decisiones", "Ayúdame a reflexionar sobre una decisión con una carta imaginaria."), new("Metas", "Quiero reflexionar sobre mis metas con una tirada ficticia.")],
        ChatMode.Comedy => [new("Chiste", "Chiste aleatorio"), new("Otro chiste", "Chiste aleatorio"),
            new("Improvisación", "Improvisa una escena cómica en una universidad con un unicornio perdido."), new("Historia absurda", "Hagamos una historia absurda juntos, una frase por turno.")],
        ChatMode.Free => [new("Mi día", "Quiero contarte mi día"), new("Aventura espacial", "Aventura espacial"),
            new("Anime y gustos", "Hablemos de nuestros gustos de anime, pregúntame cuál me gusta."), new("Lluvia de ideas", "Ayúdame a inventar una idea divertida para un proyecto.")],
        ChatMode.Gamer => [new("Vasos", "@cups"), new("Póker", "@poker"),
            new("Reglas", "Explícame las reglas de los minijuegos y sus fichas ficticias."), new("Charla gamer", "Hablemos de videojuegos y desafíos divertidos.")],
        _ => []
    };

    internal bool TryReply(ChatMode mode, string message, out string reply)
    {
        string text = message.Trim().ToLowerInvariant().TrimEnd('.', '!', '?');
        reply = "";
        if (text == "salir actividad")
        {
            progress.Remove(mode); scores.Remove(mode);
            reply = "Actividad cerrada. Podemos conversar o empezar otra con los botones."; return true;
        }
        if (mode == ChatMode.Math && text == "reto guiado")
        { progress[mode] = 0; reply = MathQuestions[0] + "\nEscribe salir actividad para terminar."; return true; }
        if (mode == ChatMode.English && text == "quiz de verbos")
        { progress[mode] = 0; scores[mode] = 0; reply = EnglishQuestions[0] + "\nResponde solo la palabra. Puedes escribir salir actividad."; return true; }
        if (mode == ChatMode.Free && text == "aventura espacial")
        { progress[mode] = 0; reply = "Tu nave se detiene frente a una luna violeta. BAEL lleva un casco con cuerno de unicornio. ¿Exploras el cráter o envías una señal? Responde cráter o señal. Puedes escribir salir actividad."; return true; }
        if (mode == ChatMode.Tarot && (text == "tirada de 3 cartas" || text == "carta del día"))
        {
            var cards = Cards.OrderBy(_ => Random.Shared.Next()).Take(text == "carta del día" ? 1 : 3).ToArray();
            reply = "Tirada imaginaria para reflexionar:\n" + string.Join("\n", cards.Select((c, i) => $"{i+1}. {c.Name}: {c.Question}")) + "\nElige una pregunta para conversar; estas cartas no predicen el futuro."; return true;
        }
        if (mode == ChatMode.Comedy && text == "chiste aleatorio")
        { reply = Jokes[Random.Shared.Next(Jokes.Length)]; return true; }
        if (!progress.TryGetValue(mode, out int step)) return false;
        if (mode == ChatMode.Math)
        {
            int[] expected = [4, 2, 5];
            if (!int.TryParse(text, out int number) || number != expected[step])
            { reply = "Pista: busca la operación inversa que mantiene la igualdad. No hace falta calcular la incógnita.\n" + MathQuestions[step]; return true; }
            step++;
            if (step == 3) { progress.Remove(mode); reply = "¡Tres pasos bien razonados! Ahora termina los despejes en tu cuaderno y comprueba sustituyendo cada valor. Puedes compartir tu procedimiento para revisarlo juntos."; }
            else { progress[mode] = step; reply = "Correcto: aplica esa operación en ambos lados.\n" + MathQuestions[step]; }
            return true;
        }
        if (mode == ChatMode.English)
        {
            if (text != EnglishAnswers[step])
            { reply = "Inténtalo otra vez. " + Explanations[step] + "\n" + EnglishQuestions[step]; return true; }
            scores[mode]++;
            reply = "¡Correcto! " + Explanations[step];
            if (++step == EnglishQuestions.Length)
            { progress.Remove(mode); reply += $"\nCompletaste {scores[mode]} de 4 ejercicios. Ahora escribe una frase propia con uno de estos verbos, o inicia otro quiz."; }
            else { progress[mode] = step; reply += "\n" + EnglishQuestions[step]; }
            return true;
        }
        if (mode == ChatMode.Free)
        {
            if (step == 0)
            {
                if (text is not ("cráter" or "crater" or "señal" or "senal"))
                { reply = "Elige cráter o señal para continuar la aventura."; return true; }
                progress[mode] = 1;
                reply = text.StartsWith("cr") ? "En el cráter encuentras una puerta que ronca. BAEL propone tocar con educación. ¿Tocas o cantas?" : "Tu señal recibe una receta de galletas estelares. Un robot llega con una bandeja vacía. ¿Tocas su timbre o cantas para saludar? Responde tocas o cantas.";
            }
            else
            {
                if (text is not ("tocas" or "cantas")) { reply = "BAEL espera tu decisión: escribe tocas o cantas."; return true; }
                progress.Remove(mode);
                reply = text == "cantas" ? "Tu canción despierta a la luna: resulta ser un unicornio dormido. BAEL negocia un viaje de regreso a cambio de no volver a cantar. Fin de esta aventura. ¿Inventamos juntos el siguiente capítulo?" : "La puerta abre una cafetería intergaláctica. BAEL consigue chocolate caliente y un mapa de regreso. Misión completada: a veces la solución era tocar. ¿Qué nombre le pondrías a esta aventura?";
            }
            return true;
        }
        return false;
    }

    internal static void Verify()
    {
        foreach (var mode in Enum.GetValues<ChatMode>())
            if (Catalog(mode).Length < 4 || string.IsNullOrWhiteSpace(Description(mode)))
                throw new InvalidOperationException("Faltan actividades de un modo.");
        var run = new ModeActivities();
        if (!run.TryReply(ChatMode.Math, "Reto guiado", out _) ||
            !run.TryReply(ChatMode.Math, "9", out string hint) || !hint.Contains("Pista") ||
            !run.TryReply(ChatMode.Math, "4", out string next) || !next.Contains("2/3"))
            throw new InvalidOperationException("El reto de matemáticas perdió su progresión.");
        run.TryReply(ChatMode.English, "Quiz de verbos", out _);
        foreach (string answer in EnglishAnswers) run.TryReply(ChatMode.English, answer, out _);
        if (run.progress.ContainsKey(ChatMode.English)) throw new InvalidOperationException("El quiz no terminó.");
    }
}
