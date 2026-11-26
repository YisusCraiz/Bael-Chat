using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bael;

internal sealed record EnglishIssue(int Start,int Length,string Original,string[] Options,string Reason,string Source)
{
    public string Choice {get;set;}=Options.FirstOrDefault()??"";
    public string Category=>Source=="Sugerencia IA"?"Una propuesta para tu texto":Source.Contains("ortografía",StringComparison.OrdinalIgnoreCase)?"Revisa esta palabra":"Un detalle de gramática";
}
internal sealed record WordMatch(string Expected,string Heard,string Kind)
{
    public string Label=>Kind=="extra"?"+ "+Heard:Expected;
    public string Background=>Kind=="ok"?"#E1F2E4":Kind=="extra"?"#FFF0D5":"#FBE2E2";
    public string Foreground=>"#302847";
    public string Detail=>Kind switch { "ok"=>"Coincide", "missing"=>"No apareció en la transcripción", "extra"=>"Palabra adicional reconocida",_=>$"Esperada: {Expected} · reconocida: {Heard}" };
}
internal sealed record ReadingResult(int Score,WordMatch[] Words)
{
    public string Feedback=>Words.All(w=>w.Kind=="ok")?"Todas las palabras coinciden. Esto no evalúa sonidos, acento ni entonación.":
        "Vuelve a escuchar y practicar: "+string.Join(", ",Words.Where(w=>w.Kind is "missing" or "changed").Select(w=>w.Expected).Distinct().Take(15))+". Confirma primero que la transcripción represente lo que dijiste; el reconocimiento puede equivocarse.";
}
internal static class EnglishPractice
{
    private static readonly (string Pattern,string Replacement,string Reason)[] Rules=
    [(@"\bI is\b","I am","Con I usa am, no is."),(@"\bI has\b","I have","Con I usa have."),
     (@"\b(he|she|it) are\b","$1 is","Con he/she/it usa is."),(@"\b(he|she|it) have\b","$1 has","Con he/she/it usa has en presente afirmativo."),
     (@"\b(he|she|it) don't\b","$1 doesn't","Con he/she/it usa doesn't."),(@"\b(you|we|they) is\b","$1 are","Con you/we/they usa are."),
     (@"\bdidn't went\b","didn't go","Después de didn't usa el verbo base."),(@"\b(can|should|must) to\b","$1","Después de can/should/must usa el verbo sin to."),
     (@"\bmore better\b","better","Better ya es comparativo."),(@"\bvery gooder\b","much better","Usa much better para este comparativo."),
     (@"\bi\b","I","El pronombre I se escribe en mayúscula.")];
    private static readonly Dictionary<string,string> Typos=new(StringComparer.OrdinalIgnoreCase)
    { ["becuase"]="because",["beacuse"]="because",["freind"]="friend",["recieve"]="receive",["adress"]="address",["teh"]="the",["studing"]="studying",["writting"]="writing",["langauge"]="language",["realy"]="really",["thier"]="their",["definately"]="definitely",["tomorow"]="tomorrow",["yestarday"]="yesterday",["engish"]="English" };
    internal static EnglishIssue[] Review(string text)
    {
        var issues=new List<EnglishIssue>();
        foreach(var rule in Rules)
        {
            var options=rule.Pattern==@"\bi\b"?RegexOptions.None:RegexOptions.IgnoreCase;
            foreach(Match match in Regex.Matches(text,rule.Pattern,options))
                issues.Add(new(match.Index,match.Length,match.Value,[Regex.Replace(match.Value,rule.Pattern,rule.Replacement,options)],rule.Reason,"Regla local"));
        }
        foreach(Match word in Regex.Matches(text,@"\b[A-Za-z]+\b"))
            if(Typos.TryGetValue(word.Value,out var corrected)) issues.Add(new(word.Index,word.Length,word.Value,[corrected],"Revisa la escritura de esta palabra.","Ortografía local"));
        return issues.DistinctBy(i=>(i.Start,i.Length)).OrderBy(i=>i.Start).Take(30).ToArray();
    }
    internal static string[] Words(string text)=>Regex.Matches(text.Replace('’','\'').ToLowerInvariant(),@"[a-z]+(?:'[a-z]+)?").Select(m=>m.Value).Take(200).ToArray();
    internal static ReadingResult Align(string reference,string heard)
    {
        string[] a=Words(reference),b=Words(heard); int[,] cost=new int[a.Length+1,b.Length+1];
        for(int i=0;i<=a.Length;i++) cost[i,0]=i;
        for(int j=0;j<=b.Length;j++) cost[0,j]=j;
        for(int i=1;i<=a.Length;i++) for(int j=1;j<=b.Length;j++) cost[i,j]=Math.Min(cost[i-1,j]+1,Math.Min(cost[i,j-1]+1,cost[i-1,j-1]+(a[i-1]==b[j-1]?0:1)));
        var result=new List<WordMatch>(); int x=a.Length,y=b.Length,correct=0;
        while(x>0 || y>0)
        {
            if(x>0 && y>0 && cost[x,y]==cost[x-1,y-1]+(a[x-1]==b[y-1]?0:1))
            { bool same=a[x-1]==b[y-1]; if(same) correct++; result.Add(new(a[--x],b[--y],same?"ok":"changed")); }
            else if(x>0 && cost[x,y]==cost[x-1,y]+1) result.Add(new(a[--x],"","missing"));
            else result.Add(new("",b[--y],"extra"));
        }
        result.Reverse(); return new(a.Length==0?0:(int)Math.Round(100.0*correct/Math.Max(a.Length,b.Length)),result.ToArray());
    }
    internal static bool TryAiReview(string output,string source,out EnglishIssue[] issues)
    {
        issues=[];
        try
        {
            output=output.Trim(); if(output.StartsWith("```")) { int start=output.IndexOf('\n'),end=output.LastIndexOf("```",StringComparison.Ordinal); if(start<0 || end<=start) return false; output=output[(start+1)..end]; }
            if(output.Length>15000) return false;
            using var json=JsonDocument.Parse(output);
            if(json.RootElement.ValueKind!=JsonValueKind.Object || !json.RootElement.TryGetProperty("issues",out var rows) || rows.ValueKind!=JsonValueKind.Array || rows.GetArrayLength()>12) return false;
            var found=new List<EnglishIssue>();
            foreach(var row in rows.EnumerateArray())
            {
                if(row.ValueKind!=JsonValueKind.Object || !row.TryGetProperty("original",out var o) || o.ValueKind!=JsonValueKind.String
                    || !row.TryGetProperty("replacement",out var r) || r.ValueKind!=JsonValueKind.String || !row.TryGetProperty("reason",out var why) || why.ValueKind!=JsonValueKind.String) return false;
                string original=o.GetString()!,replacement=r.GetString()!,reason=why.GetString()!;
                int start=source.IndexOf(original,StringComparison.Ordinal);
                if(original.Length is <1 or >180 || replacement.Length is <1 or >240 || reason.Length is <3 or >400 || start<0
                    || source.IndexOf(original,start+original.Length,StringComparison.Ordinal)>=0) return false;
                found.Add(new(start,original.Length,original,[replacement],reason,"Sugerencia IA"));
            }
            issues=found.ToArray(); return true;
        }
        catch(JsonException) { return false; }
    }
    internal static (int Score,string Feedback) Grade(EnglishQuestion[] questions,int[] selected)
    {
        if(selected.Length!=questions.Length || selected.Any(i=>i<0 || i>=3)) throw new InvalidOperationException("Responde todas las preguntas antes de evaluar.");
        int right=0; var lines=new List<string>();
        for(int i=0;i<questions.Length;i++)
        {
            var q=questions[i]; bool ok=selected[i]==q.Correct; if(ok) right++;
            lines.Add($"{i+1}. {(ok?"Correcto":"Por repasar")} · Tu respuesta: {q.Options[selected[i]]}\nRespuesta: {q.Options[q.Correct]}\n{q.Why}");
        }
        return ((int)Math.Round(100.0*right/questions.Length),$"{right}/{questions.Length} respuestas correctas.\n\n"+string.Join("\n\n",lines));
    }

    internal static bool MeetsStep(EnglishStep step,string text)=>Words(text).Length>=4 && Words(text).Any(word=>step.Keywords.Contains(word,StringComparer.OrdinalIgnoreCase));
}
