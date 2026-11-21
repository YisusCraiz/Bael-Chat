using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Bael;

internal sealed record PdfEntry(string Id, string Name, int Pages)
{
    public string Label => $"{Name} · {Pages} páginas";
}
internal sealed record PdfPageText(int Page, string Text);
internal sealed record DocumentRule
{
    public bool Limited { get; init; }
    public bool Assisted { get; init; }
    public List<string> DocumentIds { get; init; } = [];
}

internal static class PdfLibrary
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BAEL", "Documents");

    public static PdfEntry Import(string source, CancellationToken cancellationToken)
    {
        string id = Guid.NewGuid().ToString("N");
        string folder = Path.Combine(Root, id);
        Directory.CreateDirectory(folder);
        try
        {
            string local = Path.Combine(folder, "document.pdf");
            File.Copy(source, local);
            using var pdf = PdfDocument.Open(local);
            var pages = new List<PdfPageText>();
            foreach (var page in pdf.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                string text = ContentOrderTextExtractor.GetText(page, true);
                pages.Add(new(page.Number, text));
            }

            if (pages.All(page => string.IsNullOrWhiteSpace(page.Text)))
                throw new InvalidDataException("El PDF no contiene texto seleccionable. Necesita OCR antes de importarlo.");
            File.WriteAllText(Path.Combine(folder, "pages.json"), JsonSerializer.Serialize(pages));
            cancellationToken.ThrowIfCancellationRequested();
            return new(id, Path.GetFileName(source), pages.Count);
        }
        catch
        {
            Remove(id);
            throw;
        }
    }

    public static void Remove(string id)
    {
        if (!Regex.IsMatch(id, "^[a-f0-9]{32}$"))
            return;
        lock(CacheSync) Cache.Remove(id);
        try
        {
            string folder = Path.Combine(Root, id);
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The configuration can stop referencing a document even if Windows holds a file open.
        }
    }

    private static readonly object CacheSync=new();
    private static readonly Dictionary<string,(DateTime Stamp,PdfPageText[] Pages)> Cache=new();
    internal static PdfPageText[] Read(PdfEntry entry)
    {
        if (!Regex.IsMatch(entry.Id, "^[a-f0-9]{32}$"))
            return [];
        try
        {
            string path=Path.Combine(Root,entry.Id,"pages.json");
            if(!File.Exists(path)) { lock(CacheSync) Cache.Remove(entry.Id); return []; }
            DateTime stamp=File.GetLastWriteTimeUtc(path);
            lock(CacheSync) if(Cache.TryGetValue(entry.Id,out var cached) && cached.Stamp==stamp) return cached.Pages;
            var pages=JsonSerializer.Deserialize<PdfPageText[]>(File.ReadAllText(path)) ?? [];
            lock(CacheSync)
            { if(Cache.Count>=8) Cache.Remove(Cache.Keys.First()); Cache[entry.Id]=(stamp,pages); }
            return pages;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public static string Answer(BaelSettings settings,ChatMode mode,string question) =>
        PdfStudy.Retrieve(settings,mode,question,CancellationToken.None).Extracts(mode);

    internal sealed record Passage(string Name, int Page, string Text, int Score);

    internal static Passage[] Search(IEnumerable<(string Name,int Page,string Text)> pages,string question) =>
        PdfStudy.Rank(pages.Select(p=>("",p.Name,p.Page,p.Text)),question).Sources.Take(3)
            .Select(s=>new Passage(s.Name,s.Page,s.Text,PdfStudy.Terms(s.Text).Intersect(PdfStudy.Terms(question)).Count())).ToArray();

    internal static string Clean(string text) => Regex.Replace(Regex.Replace(text.Replace("\u00ad",""),@"(?<=\p{L})-\s*\r?\n\s*(?=\p{L})",""),@"\s+"," ").Trim();
    internal static IEnumerable<string> Chunks(string text)
    {
        string normalized=Clean(text);
        for(int offset=0;offset<normalized.Length;)
        {
            int count=Math.Min(650,normalized.Length-offset);
            if(offset+count<normalized.Length)
            { int space=normalized.LastIndexOf(' ',offset+count-1,count); if(space>offset) count=space-offset; }
            if(count>0) yield return normalized.Substring(offset,count).Trim();
            if(offset+count>=normalized.Length) yield break;
            offset+=Math.Max(1,count-80);
            while(offset<normalized.Length && normalized[offset]!=' ') offset++;
            while(offset<normalized.Length && normalized[offset]==' ') offset++;
        }
    }

    internal static void Verify()
    {
        string source = Path.Combine(Path.GetTempPath(), $"bael-pdf-check-{Guid.NewGuid():N}.pdf");
        PdfEntry? imported = null;
        try
        {
            var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            builder.AddPage(PageSize.A4).AddText("La fibra transporta luz.", 12, new PdfPoint(25, 700), font);
            File.WriteAllBytes(source, builder.Build());
            imported = Import(source, CancellationToken.None);
            var localSettings = new BaelSettings
            {
                Documents = [imported],
                DocumentRules = new() { [ChatMode.Student] = new() { Limited = true, DocumentIds = [imported.Id] } }
            };
            string answer = Answer(localSettings, ChatMode.Student, "fibra");
            if (imported.Pages != 1 || !answer.Contains("fibra transporta luz") || !answer.Contains("página 1"))
                throw new InvalidOperationException("La importación PDF no conservó el texto y la página.");
            var restored = JsonSerializer.Deserialize<BaelSettings>(JsonSerializer.Serialize(localSettings))!;
            if (!restored.GetDocumentRule(ChatMode.Student).DocumentIds.Contains(imported.Id))
                throw new InvalidOperationException("La asignación de PDF no se conservó.");
            Remove(imported.Id);
            if (!Answer(localSettings, ChatMode.Student, "fibra").Contains("No se pudieron leer"))
                throw new InvalidOperationException("Un documento retirado permitió una respuesta sin fuente.");
        }
        finally
        {
            if (imported is not null) Remove(imported.Id);
            File.Delete(source);
        }

        var pages = new[]
        {
            ("Redes.pdf", 2, "La fibra óptica transporta información mediante pulsos de luz."),
            ("Redes.pdf", 4, "El cobre transmite señales eléctricas.")
        };
        var results = Search(pages, "¿Cómo transmite la fibra óptica?");
        if (results.Length == 0 || results[0].Page != 2 || results[0].Name != "Redes.pdf")
            throw new InvalidOperationException("La consulta perdió el documento o la página.");
        if (Search(pages, "Fotosíntesis clorofila").Length != 0)
            throw new InvalidOperationException("La consulta inventó información fuera del material.");
        var settings = new BaelSettings();
        if (!settings.GetDocumentRule(ChatMode.Student).Limited || settings.GetDocumentRule(ChatMode.Comedy).Limited)
            throw new InvalidOperationException("No se respetaron las reglas iniciales por modo.");
        if (!Answer(settings, ChatMode.Student, "fibra").Contains("Añade PDF"))
            throw new InvalidOperationException("El modo limitado respondió sin documentos.");
    }
}
