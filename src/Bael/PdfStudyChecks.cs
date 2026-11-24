using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Bael;

internal static class PdfStudyChecks
{
    internal static async Task RunAsync()
    {
        var pages=new[]
        {
            ("a","Redes.pdf",2,"La fibra óptica transporta información mediante pulsos de luz."),
            ("a","Redes.pdf",4,"El cobre transmite señales eléctricas."),
            ("b","Otro.pdf",1,"Los planetas giran alrededor del sol.")
        };
        var compared=PdfStudy.Rank(pages,"Compara fibra y cobre");
        if(!compared.Complete || !compared.Sources.Any(s=>s.Page==2) || !compared.Sources.Any(s=>s.Page==4)
            || compared.Sources.Any(s=>s.Name=="Otro.pdf")) Fail("La comparación perdió fuentes relevantes o mezcló temas.");
        string brief=PdfStudy.Brief(compared,"Compara fibra y cobre");
        if(brief.Length>1000 || !brief.Contains("página 2") || !brief.Contains("página 4")) Fail("El resumen breve perdió sus fuentes.");
        var longEvidence=PdfStudy.Rank(new[] { ("a","Largo.pdf",1,string.Join(" ",Enumerable.Repeat("Información sobre fibra óptica y señales",180))) },"fibra");
        if(PdfStudy.Brief(longEvidence,"fibra").Length>1100) Fail("El resumen devolvió páginas completas.");
        var noisy=PdfStudy.Rank(new[] { ("a","Mezcla.pdf",1,"La fibra transporta luz. Los planetas giran alrededor del sol. Las plantas necesitan agua.") },"fibra");
        string focused=PdfStudy.Brief(noisy,"fibra");
        if(!focused.Contains("fibra transporta luz") || focused.Contains("planetas") || focused.Contains("plantas")) Fail("El resumen mezcló frases ajenas a la pregunta.");
        var partial=PdfStudy.Rank(pages,"fibra fotosíntesis");
        if(partial.Complete || !partial.Extracts(ChatMode.Student).Contains("parciales")) Fail("Una coincidencia parcial permitió síntesis sin respaldo.");
        if(PdfStudy.Rank(pages,"clorofila").Sources.Length!=0) Fail("Se inventó respaldo documental.");
        if(!PdfStudy.Rank(pages,"Buscar en mis PDF: fibra óptica").Complete) Fail("El acceso Buscar agregó términos ajenos.");
        var summary=PdfStudy.Rank(Enumerable.Range(1,10).Select(i=>("a","Redes.pdf",i,"La fibra transporta información.")),"Resume mis PDF seleccionados");
        if(!summary.Complete || summary.Task!=PdfTask.Summary || summary.Sources.Length!=6 || summary.Sources.Select(s=>s.Page).Distinct().Count()!=6)
            Fail("El resumen no respetó su límite o sus páginas.");
        using(var canceled=new CancellationTokenSource())
        {
            canceled.Cancel();
            try { PdfStudy.Rank(pages,"fibra",canceled.Token); Fail("La búsqueda ignoró cancelación."); }
            catch(OperationCanceledException) { }
        }
        string id=new('a',32);
        var source=new PdfSource("S1",id,"Redes.pdf",2,pages[0].Item4);
        string good=JsonSerializer.Serialize(new { sections=new[] { new { explanation="La fibra utiliza pulsos de luz.",source_id="S1",quote=source.Text } } });
        if(!PdfStudy.TryExplanation(good,[source],out var answer) || !answer.Contains("Redes.pdf, página 2"))
            Fail("Se perdió una explicación con cita válida.");
        foreach(string invalid in new[] { "texto sin formato","[]","null","3","{\"sections\":[3]}","{\"sections\":[{}]}",good.Replace("S1","S99") })
            if(PdfStudy.TryExplanation(invalid,[source],out _)) Fail("Se aceptó una cita o estructura inválida.");
        // JsonSerializer escapes accents: create fabricated quote explicitly rather than replacing serialized Unicode.
        string fabricated=JsonSerializer.Serialize(new { sections=new[] { new { explanation="Una explicación inventada.",source_id="S1",quote="Una cita inventada con suficiente longitud." } } });
        if(PdfStudy.TryExplanation(fabricated,[source],out _)) Fail("Se aceptó un pasaje inexistente.");
        var settings=new BaelSettings { OnlineChat=true,Profiles=[new() { Provider="Groq",Model="test",Key="fake" }],Documents=[new(id,"Redes.pdf",4)],
            DocumentRules=new() { [ChatMode.Student]=new() { Limited=true,Assisted=true,DocumentIds=[id] } } };
        var restored=JsonSerializer.Deserialize<BaelSettings>(JsonSerializer.Serialize(settings))!;
        if(!restored.GetDocumentRule(ChatMode.Student).Assisted || new DocumentRule().Assisted) Fail("El permiso documental no conservó su valor o se activó solo.");
        using var handler=new InspectHandler(good); using var client=new HttpClient(handler); var router=new ChatRouter(client);
        var result=await router.ReplyFromDocumentsAsync(settings,ChatMode.Student,"fibra",[source],CancellationToken.None);
        if(result.Text!=good || !PdfStudy.TryExplanation(result.Text,[source],out _)) Fail("La respuesta documental no recorrió el proveedor.");
        using(var body=JsonDocument.Parse(handler.Body))
        {
            var messages=body.RootElement.GetProperty("messages");
            if(messages.GetArrayLength()!=2) Fail("La petición documental incluyó historial.");
            using var payload=JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
            if(payload.RootElement.GetProperty("question").GetString()!="fibra" || payload.RootElement.GetProperty("sources").GetArrayLength()!=1
                || payload.RootElement.GetProperty("sources")[0].GetProperty("text").GetString()!=source.Text) Fail("La petición perdió sus fuentes asignadas.");
        }
        int calls=handler.Calls;
        var disabled=settings with { DocumentRules=new() { [ChatMode.Student]=new() { Limited=true,DocumentIds=[id] } } };
        await router.ReplyFromDocumentsAsync(disabled,ChatMode.Student,"fibra",[source],CancellationToken.None);
        await router.ReplyFromDocumentsAsync(settings with { OnlineChat=false },ChatMode.Student,"fibra",[source],CancellationToken.None);
        await router.ReplyFromDocumentsAsync(settings,ChatMode.Student,"fibra",[source with { DocumentId=new string('b',32) }],CancellationToken.None);
        await router.ReplyFromDocumentsAsync(settings,ChatMode.Student,"fibra",[source,source],CancellationToken.None);
        if(handler.Calls!=calls) Fail("Se envió un documento sin permiso o fuera del límite de fuentes.");
    }
    private static void Fail(string reason) => throw new InvalidOperationException(reason);
    private sealed class InspectHandler(string response) : HttpMessageHandler
    {
        internal string Body { get; private set; }="";
        internal int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Calls++; Body=await request.Content!.ReadAsStringAsync(token);
            return new(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(new { choices=new[] { new { message=new { content=response } } } })) };
        }
    }
}
