using System.Text.RegularExpressions;

namespace Bael;

internal sealed class PdfQuizSession
{
    private sealed record Card(string Sentence,string Hidden,string Answer,PdfSource Source);
    private Card[] cards=[];
    private int index;
    private ChatMode? mode;
    internal void Stop() { cards=[]; index=0; mode=null; }
    internal string Start(ChatMode selected,PdfEvidence evidence)
    {
        Stop(); mode=selected;
        cards=evidence.Sources.Select(MakeCard).Where(c=>c is not null).Cast<Card>().DistinctBy(c=>c.Sentence).Take(3).ToArray();
        if(cards.Length==0) { Stop(); return "No hay texto suficiente para preparar este repaso. Prueba otro tema o consulta los pasajes."; }
        return "Repaso local: completa palabras del material. Escribe pista para una ayuda o salir repaso para terminar.\n"+Question();
    }
    private static Card? MakeCard(PdfSource source)
    {
        string sentence=Regex.Split(source.Text,@"(?<=[.!?])\s+").FirstOrDefault(s=>s.Length is >=18 and <=320) ?? source.Text;
        if(sentence.Length>320) { sentence=sentence[..320]; int space=sentence.LastIndexOf(' '); if(space>0) sentence=sentence[..space]; }
        string? word=Regex.Matches(sentence,@"\p{L}{5,}").Select(m=>m.Value).Where(w=>PdfStudy.Terms(w).Count>0).OrderByDescending(w=>w.Length).FirstOrDefault();
        if(word is null) return null;
        string hidden=Regex.Replace(sentence,@"\b"+Regex.Escape(word)+@"\b","_____",RegexOptions.IgnoreCase);
        return new(sentence,hidden,word,source);
    }
    private string Question() => $"Pregunta {index+1}/{cards.Length}: completa el espacio.\n«{cards[index].Hidden}»\nFuente: {cards[index].Source.Name}, página {cards[index].Source.Page}.";
    internal bool TryReply(ChatMode selected,string message,out string reply)
    {
        reply=""; if(mode!=selected || cards.Length==0) return false;
        string answer=PdfStudy.Normalize(message).Trim().TrimEnd('.','!','?');
        if(answer is "salir repaso" or "salir actividad") { Stop(); reply="Repaso terminado. Puedes buscar otro tema."; return true; }
        if(answer=="pista") { reply=$"Empieza con {cards[index].Answer[0]} y tiene {cards[index].Answer.Length} letras.\n"+Question(); return true; }
        if(PdfStudy.Intent(message)!=PdfTask.Search || message.Contains('?') || answer.StartsWith("que ") || answer.StartsWith("como ") || answer.StartsWith("buscar "))
        { Stop(); return false; }
        answer=Regex.Replace(answer,@"^(?:la respuesta es |creo que |es |el |la )","").Trim();
        if(answer!=PdfStudy.Normalize(cards[index].Answer))
        { reply="Todavía no coincide con la palabra del pasaje. Puedes pedir pista.\n"+Question(); return true; }
        string correct=cards[index].Answer;
        if(++index==cards.Length) { Stop(); reply=$"¡Correcto: {correct}! Terminaste el repaso. Puedes iniciar otro o consultar un concepto del material."; }
        else reply=$"¡Correcto: {correct}!\n"+Question();
        return true;
    }
    internal static void Verify()
    {
        var source=new PdfSource("S1","id","Redes.pdf",3,"La fibra transporta información mediante pulsos de luz.");
        var quiz=new PdfQuizSession(); string first=quiz.Start(ChatMode.Student,new(PdfTask.Quiz,[source],true,""));
        if(!first.Contains("página 3") || !first.Contains("_____")) throw new InvalidOperationException("El repaso perdió su fuente.");
        if(!quiz.TryReply(ChatMode.Student,"pista",out var hint) || !hint.Contains("letras")) throw new InvalidOperationException("El repaso no dio una pista.");
        string expected=quiz.cards[0].Answer;
        if(!quiz.TryReply(ChatMode.Student,expected,out var end) || !end.Contains("Terminaste")) throw new InvalidOperationException("El repaso no verificó la respuesta.");
        quiz.Start(ChatMode.Student,new(PdfTask.Quiz,[source],true,""));
        if(quiz.TryReply(ChatMode.Student,"¿Qué es cobre?",out _)) throw new InvalidOperationException("El repaso bloqueó una pregunta nueva.");
    }
}
