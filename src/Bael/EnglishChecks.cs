using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Bael;
internal static class EnglishChecks
{
    internal static async Task RunAsync()
    {
        foreach(var lesson in EnglishCourse.Lessons)
        {
            if(EnglishCourse.WordCount(lesson.Listening)!=50 || lesson.Questions.Length!=3 || lesson.Steps.Length!=3 || lesson.Questions.Any(q=>q.Correct<0 || q.Correct>=q.Options.Length))
                throw new InvalidOperationException("Un tema no conservó sus 50 palabras o sus actividades.");
            var good=EnglishPractice.Grade(lesson.Questions,lesson.Questions.Select(q=>q.Correct).ToArray());
            var bad=EnglishPractice.Grade(lesson.Questions,lesson.Questions.Select(q=>(q.Correct+1)%3).ToArray());
            if(good.Score!=100 || bad.Score!=0 || !bad.Feedback.Contains("Por repasar")) throw new InvalidOperationException("La comprensión no corrigió las respuestas.");
        }
        var correct=EnglishPractice.Align("Hello, my friend!","hello my FRIEND");
        var missing=EnglishPractice.Align("I like my school","I like school");
        var extra=EnglishPractice.Align("I like school","I really like school");
        if(correct.Score!=100 || !missing.Words.Any(w=>w.Kind=="missing" && w.Expected=="my") || !extra.Words.Any(w=>w.Kind=="extra" && w.Heard=="really")) throw new InvalidOperationException("La lectura perdió la alineación por palabras.");
        if(EnglishPractice.Review("I is a student becuase I like English.").Length<2 || !EnglishPractice.Review("I can to swim.").Any(i=>i.Options.Contains("can"))) throw new InvalidOperationException("Se perdieron reglas de revisión.");
        string output=JsonSerializer.Serialize(new { issues=new[] { new { original="I is",replacement="I am",reason="Con I utiliza am." } } });
        if(!EnglishPractice.TryAiReview(output,"I is happy.",out var issues) || issues[0].Start!=0
            || EnglishPractice.TryAiReview(output,"I is happy. I is a student.",out _) || EnglishPractice.TryAiReview("[]","I is happy.",out _)) throw new InvalidOperationException("Se aplicó una corrección de IA a un fragmento inexistente o ambiguo.");
        var memory=EnglishStore.Volatile(); var window=new EnglishWindow(new BaelSettings(),memory);
        try { window.VerifyInterface(); } finally { window.Close(); }
        if(memory.Data.Attempts.Count<3 || memory.Data.Draft(EnglishCourse.Lessons[0].Id).Writing.Length==0) throw new InvalidOperationException("No se conservaron los intentos locales.");
        string folder=Path.Combine(Path.GetTempPath(),"bael-english-check-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            string path=Path.Combine(folder,"progress.dat"); var store=new EnglishStore(path); var lesson=EnglishCourse.Lessons[0];
            store.Data.Draft(lesson.Id).Writing="PRIVATE_ENGLISH_DRAFT";
            store.Data.Draft(lesson.Id).Conversation.Add(new("user","My name is Alex."));
            store.Add(lesson,"Escucha",100,"answer","feedback","Local");
            var loaded=new EnglishStore(path);
            if(loaded.Error.Length>0 || loaded.Data.Draft(lesson.Id).Writing!="PRIVATE_ENGLISH_DRAFT" || !loaded.Data.Completed(lesson,"Escucha")
                || File.ReadAllText(path).Contains("PRIVATE_ENGLISH_DRAFT")) throw new InvalidOperationException("El progreso no se recuperó cifrado.");
            File.WriteAllText(path,"corrupt-progress"); var damaged=new EnglishStore(path); damaged.Save();
            if(damaged.Error.Length==0 || File.ReadAllText(path)!="corrupt-progress") throw new InvalidOperationException("Se sobrescribió progreso ilegible.");
        }
        finally { Directory.Delete(folder,true); }
        using var client=new HttpClient(new EnglishTranscript());
        var text=await SpeechInput.TranscribeAsync(new BaelSettings { VoiceKey="fake",SpeechModel="scribe_v2" },[82,73,70,70],CancellationToken.None,client,"eng");
        if(text!="Hello my friend") throw new InvalidOperationException("La transcripción inglesa perdió el resultado.");
        if(EnglishCommands.Section("comprensión auditiva")!=3 || EnglishCommands.Section("no abras el aula de ingles") is not null) throw new InvalidOperationException("Los accesos de inglés interpretaron una negación.");
    }
    private sealed class EnglishTranscript:HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            string form=await request.Content!.ReadAsStringAsync(token);
            if(!form.Contains("language_code") || !form.Contains("eng")) throw new InvalidOperationException("No se especificó inglés al transcribir.");
            return new(HttpStatusCode.OK) { Content=new StringContent("{\"text\":\"Hello my friend\"}") };
        }
    }
}
