namespace Bael;

internal static class SystemCommands
{
    internal static int? Section(string message)
    {
        string text=PdfStudy.Normalize(message).Trim('¿','?','!','¡','.');
        if(text.StartsWith("no ")) return null;
        if(text is "diagnostico" or "diagnostico del sistema" or "abre el centro del sistema" or "abre el panel del sistema") return 0;
        if(text.Contains("buscar archivos") || text.Contains("busca archivos")) return 3;
        if(text.Contains("liberar espacio") || text.Contains("limpiar temporales")) return 4;
        if(text.Contains("revisar ram") || text.Contains("ampliar ram") || text.Contains("ranuras de ram")) return 2;
        if(text.Contains("salud del disco") || text.Contains("durabilidad del disco")) return 1;
        if(text.Contains("revisar componentes") || text.Contains("revisar la placa")) return 5;
        return null;
    }
}
