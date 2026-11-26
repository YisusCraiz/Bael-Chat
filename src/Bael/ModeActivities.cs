namespace Bael;

internal sealed record ModeActivity(string Label, string Prompt,bool Draft=false);

internal sealed class ModeActivities
{
    private readonly Dictionary<ChatMode, int> progress = new();
    internal bool IsRunning(ChatMode mode) => progress.ContainsKey(mode) || guided.ContainsKey(mode);
    internal void Stop(ChatMode mode) { progress.Remove(mode); scores.Remove(mode); guided.Remove(mode); }
    private readonly Dictionary<ChatMode, int> scores = new();
    private sealed record GuidedTurn(string Key,int Step,List<string> Answers);
    private readonly Dictionary<ChatMode,GuidedTurn> guided=new();
    private int lastJoke=-1;
    private static readonly Dictionary<(ChatMode Mode,string Key),string[]> LocalGuides=new()
    {
        [(ChatMode.Math,"fracciones guiadas")]=[
            "Paso 1/3 · En 1/4 y 2/4, el denominador indica el tamaño de los trozos. ¿Tienen el mismo tamaño? Responde sí o no.",
            "Paso 2/3 · Si los trozos tienen el mismo tamaño, ¿qué combinamos al sumar: numeradores o denominadores?",
            "Paso 3/3 · ¿Qué hacemos con el denominador común: conservar o sumar? Escribe una de esas palabras; tú calcularás el resultado en tu cuaderno."],
        [(ChatMode.Math,"álgebra guiada")]=[
            "Paso 1/3 · En 5z − 10 = 15, ¿qué operación inversa elimina el término −10: sumar o restar?",
            "Paso 2/3 · ¿Qué número aplicarías con esa operación a ambos lados? Escribe solo el número.",
            "Paso 3/3 · Tras quitar el término independiente queda 5z. ¿Entre qué número dividirías ambos lados? No calcules z todavía."],
        [(ChatMode.Free,"taller de ideas")]=[
            "Paso 1/3 · ¿Qué te gustaría crear y para quién? Describe una idea breve.",
            "Paso 2/3 · Elige un detalle que haga especial tu idea. ¿Qué podría probar una persona en menos de un minuto?",
            "Paso 3/3 · ¿Qué acción pequeña puedes hacer primero? Propón una que puedas terminar en unos 20 minutos."],
        [(ChatMode.Tarot,"reflexión de decisiones")]=[
            "Carta imaginaria: La Templanza. Paso 1/3 · ¿Qué decisión quieres explorar y qué opciones ves? Esta dinámica no elige por ti.",
            "Carta imaginaria: El Ermitaño. Paso 2/3 · ¿Qué información te falta para comparar esas opciones? Separa lo que sabes de lo que supones.",
            "Carta imaginaria: El Carro. Paso 3/3 · ¿Qué paso pequeño y reversible te ayudaría a obtener esa información?"],
        [(ChatMode.Tarot,"reflexión de metas")]=[
            "Carta imaginaria: La Estrella. Paso 1/3 · ¿Qué meta te gustaría empezar? Expresa lo que quieres lograr de forma concreta.",
            "Carta imaginaria: El Mago. Paso 2/3 · ¿Qué recurso, habilidad o apoyo ya tienes para comenzar?",
            "Carta imaginaria: La Fuerza. Paso 3/3 · ¿Qué acción realista puedes hacer esta semana para acercarte a tu meta?"],
        [(ChatMode.Comedy,"improvisación local")]=[
            "Escena 1/3 · Un unicornio entra en clase con una pizza como credencial. BAEL: «Soy alumno de intercambio… de ingredientes». Tú eres el profesor: ¿qué le respondes?",
            "Escena 2/3 · BAEL coloca la pizza en el proyector: «Mi presentación tiene ocho diapositivas comestibles». Profesor, ¿qué regla impone ahora tu clase?",
            "Escena 3/3 · El unicornio pide créditos extra por traer extra queso. Escribe la frase con la que cierras esta clase."],
        [(ChatMode.Comedy,"historia absurda local")]=[
            "Historia 1/3 · La impresora del campus se presenta a las elecciones prometiendo tinta gratis. BAEL es su jefe de campaña. Añade una frase: ¿cuál es su primer problema?",
            "Historia 2/3 · BAEL propone resolverlo con una conferencia de prensa, pero los micrófonos son plátanos. Añade una frase: ¿qué sucede?",
            "Historia 3/3 · La impresora anuncia que su último discurso saldrá en doble cara. Escribe tu frase para terminar la historia."]
    };
    private bool TryGuided(ChatMode mode,string text,string original,out string reply)
    {
        reply="";
        if(LocalGuides.TryGetValue((mode,text),out var questions))
        {
            Stop(mode);guided[mode]=new(text,0,[]);
            reply=questions[0]+"\nEscribe salir actividad cuando quieras terminar.";return true;
        }
        if(!guided.TryGetValue(mode,out var state)) return false;
        questions=LocalGuides[(mode,state.Key)];
        if(mode==ChatMode.Math)
        {
            bool valid=state.Key=="fracciones guiadas"?state.Step switch { 0=>text is "sí" or "si",1=>text is "numeradores" or "numerador",_=>text is "conservar" or "conservarlo" }:state.Step switch {0=>text=="sumar",1=>text=="10",_=>text=="5"};
            if(!valid) { reply=(state.Key=="fracciones guiadas"?"Pista: el número de abajo nombra el tamaño del trozo; el de arriba cuenta cuántos tomas.":"Pista: usa la operación inversa y mantén la igualdad aplicándola a ambos lados.")+" Vuelve a pensar tu elección.\n"+questions[state.Step];return true; }
        }
        else if(original.Trim().Length<3)
        { reply="Escribe una frase breve para continuar; no hay una respuesta que tengas que adivinar.\n"+questions[state.Step];return true; }
        string answer=original.Trim();if(answer.Length>400) answer=answer[..400]+"…";
        state.Answers.Add(answer);int step=state.Step+1;
        if(step<questions.Length)
        {
            guided[mode]=state with { Step=step };
            reply=(mode==ChatMode.Math?"Ese paso mantiene la regla que estamos practicando.":"Tu aporte: «"+answer+"».")+"\n"+questions[step];return true;
        }
        guided.Remove(mode);
        reply=mode switch
        {
            ChatMode.Math=>state.Key=="fracciones guiadas"?"Completaste los tres pasos. Ahora realiza la suma en tu cuaderno conservando el tamaño de los trozos y comprueba si tu resultado tiene sentido. Tú haces el cálculo final.":"Completaste los pasos para despejar. Ahora haz los cálculos en tu cuaderno y comprueba el valor sustituyéndolo en la ecuación original. Tú calculas la incógnita.",
            ChatMode.Free=>"Tu idea ya tiene un primer paso:\n"+string.Join("\n",state.Answers.Select((a,i)=>$"{i+1}. {a}"))+"\nEste resumen recoge tus propuestas; no es una evaluación de viabilidad. ¿Quieres preparar otra idea?",
            ChatMode.Tarot=>"Tu reflexión, en tus propias palabras:\n"+string.Join("\n",state.Answers.Select((a,i)=>$"{i+1}. {a}"))+"\nLas cartas no predicen resultados. Tú eliges qué hacer con esta reflexión.",
            _=>"¡Telón! Tus aportes a esta escena:\n"+string.Join("\n",state.Answers.Select((a,i)=>$"{i+1}. {a}"))+"\nBAEL hace una reverencia y tropieza con los créditos finales. Puedes empezar otra escena con los botones."
        };
        return true;
    }
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
        ChatMode.Math => [new("Reto guiado", "Reto guiado"), new("Fracciones", "Fracciones guiadas"),
            new("Álgebra", "Álgebra guiada"), new("Revisar intento", "Ejercicio: \nMi primer paso: ",true)],
        ChatMode.English => [new("Aula por niveles", "@english:0"), new("Escritura y corrección", "@english:1"),
            new("Escucha de 50 palabras", "@english:3"), new("Conversaciones", "@english:4")],
        ChatMode.System => [new("Centro del sistema", "@system"), new("Buscar archivos", "@files"),
            new("Liberar espacio", "@cleanup"), new("RAM y ampliación", "@ram")],
        ChatMode.Tarot => [new("Tres cartas", "Tirada de 3 cartas"), new("Carta del día", "Carta del día"),
            new("Decisiones", "Reflexión de decisiones"), new("Metas", "Reflexión de metas")],
        ChatMode.Comedy => [new("Chiste", "Chiste aleatorio"), new("Otro chiste", "Chiste aleatorio"),
            new("Improvisación", "Improvisación local"), new("Historia absurda", "Historia absurda local")],
        ChatMode.Free => [new("Mi día", "Hoy me pasó: ",true), new("Aventura espacial", "Aventura espacial"),
            new("Anime y gustos", "Quiero hablar de: ",true), new("Lluvia de ideas", "Taller de ideas")],
        ChatMode.Gamer => [new("Vasos", "@cups"), new("Póker", "@poker"),
            new("Reglas", "Reglas de los minijuegos"), new("Charla gamer", "Estoy jugando a: \nQuiero mejorar en: ",true)],
        _ => []
    };

    internal bool TryReply(ChatMode mode, string message, out string reply)
    {
        string text = message.Trim().ToLowerInvariant().TrimEnd('.', '!', '?');
        reply = "";
        if (text == "salir actividad")
        {
            Stop(mode);
            reply = "Actividad cerrada. Podemos conversar o empezar otra con los botones."; return true;
        }
        if(mode==ChatMode.Gamer && text=="reglas de los minijuegos")
        { reply="Sala de espectáculo · BAEL siempre gana y lo anuncia antes de empezar.\nSaldo: 1.000 fichas imaginarias por ventana. Elige 10, 50 o 100 por ronda; no hay compras ni dinero real. Al llegar a cero puedes recargar gratis.\nVasos: inicia, espera la mezcla y elige un vaso. La pelota se sitúa fuera de tu elección.\nPóker: recibes cinco cartas. Marca hasta tres para cambiarlas, o ninguna para conservarlas; pulsa Cambiar y revelar. BAEL muestra una escalera real preparada.\nUsa los botones Vasos o Póker para abrir la sala; cambiar de modo la cierra.";return true; }
        if(mode==ChatMode.Comedy && text=="chiste aleatorio")
        {
            Stop(mode);int choice;do { choice=Random.Shared.Next(Jokes.Length); } while(choice==lastJoke);
            lastJoke=choice;reply=Jokes[choice];return true;
        }
        if(mode==ChatMode.Math && text=="reto guiado" || mode==ChatMode.Free && text=="aventura espacial" || mode==ChatMode.Tarot && text is "tirada de 3 cartas" or "carta del día") Stop(mode);
        if(TryGuided(mode,text,message,out reply)) return true;
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
        foreach(var mode in Enum.GetValues<ChatMode>().Where(ModeGuide.Supports))
        {
            var info=ModeGuide.For(mode);
            if(info.Offers.Length!=4 || info.Offers.Any(o=>string.IsNullOrWhiteSpace(o.Steps)||string.IsNullOrWhiteSpace(o.Action))) throw new InvalidOperationException("Una guía de modo no explica sus actividades.");
        }
        foreach(var ((mode,key),questions) in LocalGuides)
        {
            var flow=new ModeActivities();flow.TryReply(mode,key,out _);
            if(!flow.IsRunning(mode)) throw new InvalidOperationException("No comenzó una actividad guiada.");
            if(mode==ChatMode.Math)
            {
                flow.TryReply(mode,"no",out string wrong);if(!wrong.Contains("Pista")) throw new InvalidOperationException("La fracción incorrecta avanzó.");
                foreach(var answer in key=="fracciones guiadas"?new[]{"sí","numeradores","conservar"}:new[]{"sumar","10","5"}) flow.TryReply(mode,answer,out _);
            }
            else foreach(var question in questions) flow.TryReply(mode,"Mi respuesta de prueba",out _);
            if(flow.IsRunning(mode)) throw new InvalidOperationException("La actividad no terminó.");
            flow.TryReply(mode,key,out _);flow.TryReply(mode,"salir actividad",out _);
            if(flow.IsRunning(mode)) throw new InvalidOperationException("No se pudo salir de una actividad.");
        }
        var jokes=new ModeActivities();jokes.TryReply(ChatMode.Comedy,"Chiste aleatorio",out string firstJoke);jokes.TryReply(ChatMode.Comedy,"Chiste aleatorio",out string secondJoke);
        if(firstJoke==secondJoke) throw new InvalidOperationException("Se repitió el chiste consecutivo.");
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
