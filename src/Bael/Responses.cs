namespace Bael;

internal enum ChatMode
{
    Student,
    Tarot,
    Comedy
}

internal static class Responses
{
    public static string For(ChatMode mode, string message) => mode switch
    {
        ChatMode.Tarot => Tarot(message),
        ChatMode.Comedy => Comedy(message),
        _ => "La consulta de documentos estará disponible en una fase posterior."
    };

    private static string Tarot(string message) =>
        $"Tirada ficticia de muestra para «{message}»: El Ermitaño. ¿Qué podrías decidir con calma hoy?";

    private static string Comedy(string message) =>
        $"Respuesta de muestra a «{message}»: en el campus hasta el descanso trae tarea.";
}
