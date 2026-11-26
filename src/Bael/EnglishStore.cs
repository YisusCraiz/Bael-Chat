using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bael;

internal sealed record EnglishAttempt(DateTimeOffset At,string Level,string Lesson,string Skill,int? Score,string Input,string Feedback,string Source)
{
    [JsonIgnore] public string Label=>$"{At.LocalDateTime:dd/MM HH:mm} · {Level} · {EnglishCourse.Get(Lesson).Title} · {Skill}{(Score is int n?$" · {n}%":"")} · {Source}";
}
internal sealed record EnglishDraft
{
    public string Writing {get;set;}="";
    public string Reading {get;set;}="";
    public string ConversationInput {get;set;}="";
    public List<ChatTurn> Conversation {get;set;}=[];
    public int Step {get;set;}
    public int[] ListeningAnswers {get;set;}=[];
    public bool Listened {get;set;}
    public bool TextSupport {get;set;}
    public bool SeenTranscript {get;set;}
    public int Replays {get;set;}
}
internal sealed record EnglishProgress
{
    public int Version {get;init;}=1;
    public string Lesson {get;set;}=EnglishCourse.Lessons[0].Id;
    public HashSet<string> Done {get;set;}=[];
    public List<EnglishAttempt> Attempts {get;set;}=[];
    public Dictionary<string,EnglishDraft> Drafts {get;set;}=new();
    public bool CloudConversation {get;set;}
    public string Voice {get;set;}="Windows local";
    public EnglishDraft Draft(string lesson)
    { if(!Drafts.TryGetValue(lesson,out var draft)) { draft=new(); Drafts[lesson]=draft; } return draft; }
    public bool Completed(EnglishLesson lesson,string skill)=>Done.Contains(lesson.Id+":"+skill);
}
internal sealed class EnglishStore
{
    private readonly string? path;
    internal EnglishProgress Data {get;private set;}=new();
    internal string Error {get;private set;}="";
    private bool blocked;
    internal static EnglishStore Volatile()=>new(null);
    internal EnglishStore():this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BAEL","english-study.dat")) { }
    internal EnglishStore(string? path)
    {
        this.path=path;
        if(path is null || !File.Exists(path)) return;
        try
        {
            if(new FileInfo(path).Length>8*1024*1024) throw new InvalidDataException();
            var data=JsonSerializer.Deserialize<EnglishProgress>(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser)) ?? throw new JsonException();
            if(data.Version!=1 || data.Drafts is null || data.Attempts is null || data.Done is null || data.Drafts.Any(p=>p.Value is null || p.Value.Conversation is null || p.Value.ListeningAnswers is null)) throw new InvalidDataException();
            data.Lesson=EnglishCourse.Get(data.Lesson).Id; data.Attempts=data.Attempts.TakeLast(300).ToList();
            foreach(var d in data.Drafts.Values)
            { d.Writing=MemoryData.Clip(d.Writing??"",3000); d.Reading=MemoryData.Clip(d.Reading??"",3000); d.ConversationInput=MemoryData.Clip(d.ConversationInput??"",2000); d.Conversation=d.Conversation.TakeLast(20).ToList(); d.Step=Math.Clamp(d.Step,0,3); }
            Data=data;
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException or CryptographicException or ArgumentException)
        { blocked=true; Error="No se pudo leer el progreso. Se conserva el archivo original y se desactiva el guardado para no sobrescribirlo."; }
    }
    internal void Save()
    {
        if(path is null || blocked) return;
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            Data.Attempts=Data.Attempts.TakeLast(300).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            byte[] plain=JsonSerializer.SerializeToUtf8Bytes(Data);
            while(plain.Length>6*1024*1024 && Data.Attempts.Count>0)
            { Data.Attempts.RemoveRange(0,Math.Min(20,Data.Attempts.Count)); plain=JsonSerializer.SerializeToUtf8Bytes(Data); }
            byte[] bytes=ProtectedData.Protect(plain,null,DataProtectionScope.CurrentUser);
            File.WriteAllBytes(temporary,bytes); File.Move(temporary,path,true); Error="";
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException) { Error="No se pudo guardar el progreso en Windows; los cambios siguen en esta sesión."; }
        finally { try { if(File.Exists(temporary)) File.Delete(temporary); } catch(Exception e) when(e is IOException or UnauthorizedAccessException) { } }
    }
    internal void Add(EnglishLesson lesson,string skill,int? score,string input,string feedback,string source)
    {
        if(EnglishCourse.Skills.Contains(skill) && (score is null || score>=70)) Data.Done.Add(lesson.Id+":"+skill);
        Data.Attempts.Add(new(DateTimeOffset.Now,lesson.Level,lesson.Id,skill,score,MemoryData.Clip(input,3000),MemoryData.Clip(feedback,3000),source)); Save();
    }
}
