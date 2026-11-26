namespace Bael;
internal static class EnglishCommands
{
    internal static int? Section(string message)
    {
        string text=PdfStudy.Normalize(message).Trim('¿','?','.','!','¡');
        if(text.StartsWith("no ")) return null;
        return text switch
        {
            "aula de ingles" or "abre el aula de ingles" or "niveles de ingles"=>0,
            "practicar escritura" or "revisar mi ingles"=>1,
            "practicar lectura" or "leer en voz alta"=>2,
            "comprension auditiva" or "practicar escucha"=>3,
            "practicar conversacion" or "escenarios de conversacion"=>4,
            _=>null
        };
    }
}
