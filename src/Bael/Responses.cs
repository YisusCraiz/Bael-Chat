using System.IO;

namespace Bael;

internal enum ChatMode
{
    Student,
    Tarot,
    Comedy,
    System,
    Math,
    English
}

internal static class Responses
{
    public static string Name(ChatMode mode) => mode switch
    {
        ChatMode.Tarot => "Tarot",
        ChatMode.Comedy => "Comedia",
        ChatMode.System => "Sistema",
        ChatMode.Math => "Matemáticas",
        ChatMode.English => "Inglés",
        _ => "Estudiante"
    };

    public static string Sprite(ChatMode mode) => mode switch
    {
        ChatMode.Tarot => "tarot",
        ChatMode.Comedy => "comedy",
        ChatMode.System => "system",
        ChatMode.Math => "math",
        ChatMode.English => "english",
        _ => "boy"
    };

    public static string Instruction(ChatMode mode) => mode switch
    {
        ChatMode.Tarot => "Eres BAEL en modo tarot ficticio. Da una tirada imaginaria breve y termina con una pregunta de reflexión. No predigas hechos.",
        ChatMode.Comedy => "Eres BAEL, comediante del campus. Responde con un remate breve sobre la situación de clase, sin atacar a personas.",
        ChatMode.System => "Eres BAEL en modo Sistema. Guía al usuario con una comprobación sencilla por turno para diagnosticar problemas de Windows. Pregunta el síntoma y el error. Solo conoces lo escrito en esta conversación. No afirmes haber examinado, ejecutado comandos o reparado la PC. Prioriza comprobaciones reversibles y explica su propósito.",
        ChatMode.Math => "Eres BAEL, tutor de matemáticas y álgebra. Ayuda al alumno a razonar: ofrece UNA pista o una pregunta por turno, revisa su intento y espera su siguiente paso. No resuelvas el ejercicio completo ni des el resultado final aunque te lo pidan. Explica el concepto y pide que el alumno haga el siguiente cálculo.",
        ChatMode.English => "Eres BAEL, tutor de inglés. Practica presente, pasado y futuro, verbos regulares e irregulares con un ejercicio breve por turno. Corrige el intento y explica en español. Para pronunciación ofrece una aproximación escrita y una frase corta para repetir. No afirmes escuchar o evaluar la voz del usuario. El botón de lectura permite escuchar tu respuesta.",
        _ => "Eres BAEL, compañero estudiante. Ayuda a organizar el estudio y explicar conceptos en español. La consulta restringida a PDF se configura en Ajustes → Documentos. En modo libre no inventes citas ni páginas."
    };

    public static string For(ChatMode mode, string message)
    {
        string text = message.ToLowerInvariant();
        return mode switch
        {
            ChatMode.Tarot => $"Tirada ficticia para «{message}»: El Ermitaño. ¿Qué podrías decidir con calma hoy?",
            ChatMode.Comedy => $"Sobre «{message}»: en el campus hasta el descanso trae tarea.",
            ChatMode.System => SystemHelp(text),
            ChatMode.Math => text.Contains('=')
                ? "Primero identifica qué operación acompaña a la incógnita. ¿Qué operación inversa puedes aplicar a ambos lados para empezar a despejarla? Escribe tu primer paso."
                : "Dime el ejercicio y qué has intentado. Empezamos identificando los datos y lo que te pide encontrar; después te doy una pista para tu siguiente paso.",
            ChatMode.English => EnglishHelp(text),
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
        if (text.Contains("pronunc"))
            return "Practiquemos «I study every day». Aproximación: «ai stádi évri déi». Escúchala con Leer en voz alta y repítela por partes. ¿Qué palabra quieres practicar después?";
        if (text.Contains("pasad") || text.Contains("past"))
            return "En pasado simple, study cambia a studied. Completa: Yesterday, I ___ English. ¿Qué forma elegirías?";
        if (text.Contains("futur") || text.Contains("will"))
            return "Con will usamos el verbo en su forma base. Completa: Tomorrow, she will ___ English. ¿Study o studies?";
        return "Practiquemos presente simple. Con he o she, study cambia a studies. Completa: She ___ English every day. Escribe tu intento, o pide pasado, futuro o pronunciación.";
    }
}
