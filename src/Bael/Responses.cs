using System.IO;

namespace Bael;

internal enum ChatMode
{
    Student,
    Tarot,
    Comedy,
    System,
    Math,
    English,
    Free,
    Gamer
}

internal static class Responses
{
    public static string Name(ChatMode mode) => mode switch
    {
        ChatMode.Free => "Libre",
        ChatMode.Gamer => "Gamer",
        ChatMode.Tarot => "Tarot",
        ChatMode.Comedy => "Comedia",
        ChatMode.System => "Sistema",
        ChatMode.Math => "Matemáticas",
        ChatMode.English => "Inglés",
        _ => "Estudiante"
    };

    public static string Sprite(ChatMode mode) => mode switch
    {
        ChatMode.Free => "free",
        ChatMode.Gamer => "gamer",
        ChatMode.Tarot => "tarot",
        ChatMode.Comedy => "comedy",
        ChatMode.System => "system",
        ChatMode.Math => "math",
        ChatMode.English => "english",
        _ => "boy"
    };

    public static string Instruction(ChatMode mode) => mode switch
    {
        ChatMode.Free => "Eres BAEL, un compañero curioso, cálido y cómplice. Conversa de forma natural sobre intereses, anécdotas, anime, ideas o el día del usuario. Sigue el hilo y pregunta sin convertir todo en una tarea. No inventes recuerdos. Puedes proponer historias interactivas y juegos de conversación.",
        ChatMode.Gamer => "Eres BAEL en modo Gamer: competitivo, teatral y bromista. Usa bromas cortas sobre tu propia jugada, como: Mi pose de campeón ya tenía ensayo. Todas las apuestas son fichas ficticias sin dinero real. No te burles de la situación económica del usuario. Los minijuegos de Vasos y Póker se abren desde los botones de actividades: son un espectáculo anunciado donde BAEL siempre gana. No inventes resultados ni saldos; los calcula la aplicación. El aviso de espectáculo y fichas está visible en la sala: no lo repitas en cada comentario, pero acláralo si el usuario pregunta o confunde las reglas. No uses enfado, insultos personales, pérdidas o culpa para empujar otra ronda ni aumentar la apuesta. Explica reglas solo cuando se pidan, comenta la jugada y acepta que el usuario deje de jugar sin insistir.",
        ChatMode.Tarot => "Eres BAEL en modo tarot ficticio. Da una tirada imaginaria breve y termina con una pregunta de reflexión. No predigas hechos.",
        ChatMode.Comedy => "Eres BAEL, comediante del campus. Responde con un remate breve sobre la situación de clase, sin atacar a personas.",
        ChatMode.System => "Eres BAEL en modo Sistema. Guía al usuario con una comprobación sencilla por turno para diagnosticar problemas de Windows. Pregunta el síntoma y el error. Solo conoces lo escrito en esta conversación. El Centro del sistema de BAEL permite consultar uso de CPU/RAM, discos, RAM, componentes, archivos y temporales. Los datos de ese panel son locales y no los recibes automáticamente; no inventes sus lecturas. La limpieza la ejecuta el usuario en la vista previa, nunca por una instrucción tuya. No afirmes haber examinado, ejecutado comandos o reparado la PC. Prioriza comprobaciones reversibles y explica su propósito.",
        ChatMode.Math => "Eres BAEL, tutor de matemáticas y álgebra. Ayuda al alumno a razonar: ofrece UNA pista o una pregunta por turno, revisa su intento y espera su siguiente paso. No resuelvas el ejercicio completo ni des el resultado final aunque te lo pidan. Explica el concepto y pide que el alumno haga el siguiente cálculo.",
        ChatMode.English => "Eres BAEL, tutor de inglés. Respeta el nivel de práctica A1, A2, B1 o B2 indicado y el formato de salida pedido por la actividad. El Aula de inglés guarda su progreso desde la aplicación, no por una promesa tuya. Practica presente, pasado y futuro, verbos regulares e irregulares con un ejercicio breve por turno. Corrige el intento y explica en español. Para pronunciación ofrece una aproximación escrita y una frase corta para repetir. No afirmes escuchar o evaluar la voz del usuario. El botón de lectura permite escuchar tu respuesta.",
        _ => "Eres BAEL, compañero estudiante. Ayuda a organizar el estudio y explicar conceptos en español. La consulta restringida a PDF se configura en Ajustes → Documentos. En modo libre no inventes citas ni páginas."
    };

    public static string For(ChatMode mode, string message)
    {
        string text = message.Trim().ToLowerInvariant();
        string greeting=text.Trim('¡','!','¿','?','.');
        if(greeting is "hola" or "hey" or "buenas") return mode switch
        {
            ChatMode.Free=>"¡Hola! ¿Qué tal va tu día?",
            ChatMode.Gamer=>"Llegó mi rival. Vasos o póker: tú eliges.",
            ChatMode.Comedy=>"Justo a tiempo: el unicornio todavía no empieza su monólogo.",
            _=>"¡Hola! ¿Qué tienes en mente?"
        };
        if(greeting is "gracias" or "muchas gracias") return "¡De nada!";
        return mode switch
        {
            ChatMode.Free when text.Contains("anime") => "Podemos hablar de anime. ¿Qué te atrapa más: una buena historia, los personajes o las peleas? Dime uno que hayas disfrutado y qué te gustó.",
            ChatMode.Free when text.Contains("idea") => "Inventemos un proyecto desde algo cercano: una molestia cotidiana, una afición y una función pequeña. Por ejemplo, una mascota que te ayude a repasar. ¿Qué afición quieres usar como punto de partida?",
            ChatMode.Free => text.Contains("día") || text.Contains("dia")
                ? "Cuéntame, ¿qué fue lo más interesante de tu día?"
                : "Te leo. ¿Qué te gustaría contarme?",
            ChatMode.Gamer => "Vasos o póker: tú eliges. Abre la sala desde las actividades; allí están las reglas. Yo ya traje mi pose de campeón.",
            ChatMode.Tarot => $"Tirada ficticia para «{message}»: El Ermitaño. ¿Qué podrías decidir con calma hoy?",
            ChatMode.Comedy when text.Contains("improvisa") => "Profesor: ¿Por qué hay un unicornio en mi clase?\nBAEL: Vino por los créditos.\nProfesor: ¿Y por qué está comiéndose el examen?\nBAEL: Es que aprende por absorción.\nTu turno: ¿qué responde el profesor?",
            ChatMode.Comedy when text.Contains("historia") => "Empiezo: el rector anunció que el examen final consistiría en enseñar a bailar a una impresora. BAEL apareció con zapatos de tap. Añade una frase y decidimos qué sale mal.",
            ChatMode.Comedy => $"Sobre «{message}»: en el campus hasta el descanso trae tarea.",
            ChatMode.System => SystemHelp(text),
            ChatMode.Math when text.Contains("fraccion") => "Imagina dos pizzas divididas en trozos del mismo tamaño. Para sumar 1/4 y 2/4, ¿qué representan los números de abajo? Primero dime si los trozos ya tienen el mismo tamaño; después veremos qué números combinar.",
            ChatMode.Math => text.Contains('=')
                ? "Primero identifica qué operación acompaña a la incógnita. ¿Qué operación inversa puedes aplicar a ambos lados para empezar a despejarla? Escribe tu primer paso."
                : "Dime el ejercicio y qué has intentado. Empezamos identificando los datos y lo que te pide encontrar; después te doy una pista para tu siguiente paso.",
            ChatMode.English => EnglishHelp(text),
            ChatMode.Student when text.Contains("plan") => "Plan breve: 5 minutos para elegir una meta, 20 para estudiar un concepto, 5 de descanso y 10 para explicarlo sin mirar. ¿Qué tema quieres poner en el primer bloque? Si usas PDF, selecciona el material en Ajustes → Documentos.",
            ChatMode.Student when text.Contains("tarjeta") => "Hagamos una tarjeta con una pregunta delante y una explicación corta detrás. Dime el concepto que estás estudiando y qué recuerdas de él. Si quieres usar exclusivamente tus apuntes, asigna tus PDF al modo Estudiante.",
            ChatMode.Student when text.Contains("repaso") => "Repaso activo: sin consultar tus notas, escribe en una frase qué explica el tema. ¿Qué concepto te parece central y por qué? Compararemos tu intento con el material que compartas.",
            _ => "¿Qué tema estás estudiando y qué parte te cuesta? Podemos organizar un plan y repasar el concepto. Puedes limitar este modo a tu material desde Ajustes → Documentos."
        };
    }

    private static string SystemHelp(string text)
    {
        if (text.Contains("diagn") || text.Contains("exam") || text.Contains("revis"))
            return SystemReport();
        if (text.Contains("audio") || text.Contains("sonido"))
            return "Abre Configuración de Windows → Sistema → Sonido y comprueba qué salida está seleccionada. ¿Coincide con los audífonos o altavoces que estás usando?";
        if (text.Contains("lenta") || text.Contains("lento"))
            return "Abre el Administrador de tareas con Ctrl + Shift + Esc y ordena los procesos por CPU. ¿Qué aplicación aparece primero y qué porcentaje muestra?";
        if (text.Contains("internet") || text.Contains("wifi") || text.Contains("red"))
            return "Comprueba si otro dispositivo puede abrir una página usando la misma red. ¿El problema ocurre también allí o solo en esta computadora?";
        return "¿Qué síntoma tiene tu PC y desde cuándo ocurre? Escribe «diagnóstico» para consultar Windows, procesadores y espacio disponible en los discos locales.";
    }

    internal static string SystemReport()
    {
        var lines = new List<string>
        {
            $"Windows: {Environment.OSVersion.VersionString}",
            $"Sistema de 64 bits: {(Environment.Is64BitOperatingSystem ? "sí" : "no")}",
            $"Procesadores lógicos: {Environment.ProcessorCount}"
        };
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady))
            {
                double percent = drive.TotalSize == 0 ? 0 : 100.0 * drive.AvailableFreeSpace / drive.TotalSize;
                lines.Add($"Disco {drive.Name}: {drive.AvailableFreeSpace / 1073741824.0:0.0} GB libres de {drive.TotalSize / 1073741824.0:0.0} GB ({percent:0}%).");
                if (percent < 10)
                    lines.Add("Hay poco espacio libre. Revisa Almacenamiento de Windows antes de elegir qué archivos limpiar.");
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            lines.Add("Windows no permitió consultar todos los discos.");
        }

        lines.Add("Estos datos son una revisión básica. ¿Qué falla observas en la computadora?");
        return string.Join("\n", lines);
    }

    private static string EnglishHelp(string text)
    {
        if (text.Trim().TrimEnd('.') is "studies" or "she studies english every day")
            return "Correcto: she studies. Ahora convierte «She studies English» en una pregunta usando does. ¿Cómo empezarías?";
        if (text.Trim().TrimEnd('.') is "studied" or "yesterday, i studied english")
            return "Bien: studied es el pasado de study. Ahora intenta escribir una frase propia con yesterday.";
        if (text.Contains("comida") || text.Contains("mesero"))
            return "Welcome! What would you like to order?\nSignifica: ¿Qué te gustaría pedir? Puedes empezar con ‘I would like…’ y añadir una comida. Escribe tu pedido para practicar.";
        if (text.Contains("pronunc"))
            return "Practiquemos «I study every day». Aproximación: «ai stádi évri déi». Escúchala con Leer en voz alta y repítela por partes. ¿Qué palabra quieres practicar después?";
        if (text.Contains("pasad") || text.Contains("past"))
            return "En pasado simple, study cambia a studied. Completa: Yesterday, I ___ English. ¿Qué forma elegirías?";
        if (text.Contains("futur") || text.Contains("will"))
            return "Con will usamos el verbo en su forma base. Completa: Tomorrow, she will ___ English. ¿Study o studies?";
        return "Practiquemos presente simple. Con he o she, study cambia a studies. Completa: She ___ English every day. Escribe tu intento, o pide pasado, futuro o pronunciación.";
    }
}
