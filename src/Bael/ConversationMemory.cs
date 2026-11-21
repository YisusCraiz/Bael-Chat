using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bael;

internal sealed record SavedMessage(string Text, bool IsUser, DateTimeOffset At)
{
    [JsonIgnore]
    public string Label => $"{(IsUser ? "Tú" : "BAEL")} · {At.LocalDateTime:dd/MM HH:mm}\n{Text}";
}
internal sealed record RememberedNote(string Id, ChatMode Mode, string Text, bool Enabled = true)
{
    [JsonIgnore]
    public string Label => (Enabled ? "● " : "○ ")+Text;
}
internal sealed record MemoryData
{
    public int Version { get; init; } = 1;
    public Dictionary<ChatMode,List<SavedMessage>> Messages { get; init; } = new();
    public Dictionary<ChatMode,List<ChatTurn>> Context { get; init; } = new();
    public List<RememberedNote> Notes { get; init; } = [];
    internal MemoryData Snapshot(bool keepHistory = true) => new()
    {
        Messages = keepHistory ? Messages.Where(k=>Enum.IsDefined(k.Key)).ToDictionary(k=>k.Key,
            k=>k.Value.TakeLast(100).Select(m=>m with { Text=Clip(m.Text,4000) }).ToList()) : new(),
        Context = keepHistory ? Context.Where(k=>Enum.IsDefined(k.Key)).ToDictionary(k=>k.Key,
            k=>k.Value.Where(t=>t.Role is "user" or "model").TakeLast(12).Select(t=>t with { Text=Clip(t.Text,4000) }).ToList()) : new(),
        Notes = Notes.Where(n=>Enum.IsDefined(n.Mode) && !string.IsNullOrWhiteSpace(n.Text)).GroupBy(n=>n.Mode)
            .SelectMany(g=>g.Take(20)).Select(n=>n with { Text=Clip(n.Text,500) }).ToList()
    };
    internal static string Clip(string text,int max) => text.Length<=max ? text : text[..(max-1)]+"…";
    internal string ContextFor(ChatMode mode) => JsonSerializer.Serialize(Notes.Where(n=>n.Mode==mode && n.Enabled).Take(6).Select(n=>n.Text).ToArray());
}

internal sealed class MemoryJournal
{
    private readonly string? path;
    private readonly object sync = new();
    private Task worker = Task.CompletedTask;
    private MemoryData? pending;
    private bool blocked;
    private string error = "";
    internal event Action? Completed;
    internal MemoryData Loaded { get; }
    internal bool Blocked { get { lock(sync) return blocked; } }
    internal string Error { get { lock(sync) return error; } }
    internal bool Busy { get { lock(sync) return !worker.IsCompleted; } }
    internal static MemoryJournal Volatile() => new(null);
    internal MemoryJournal() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BAEL","memory.dat")) { }
    internal MemoryJournal(string? file)
    {
        path=file; Loaded=new();
        if(path is null || !File.Exists(path)) return;
        try
        {
            if(new FileInfo(path).Length>24*1024*1024) throw new InvalidDataException();
            byte[] plain=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);
            var loaded=JsonSerializer.Deserialize<MemoryData>(plain) ?? throw new JsonException();
            if(loaded.Version!=1 || loaded.Messages is null || loaded.Context is null || loaded.Notes is null) throw new JsonException();
            Loaded=loaded.Snapshot();
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException or JsonException or ArgumentException or NullReferenceException)
        {
            blocked=true;
            error="No se pudo abrir la memoria anterior. Se conservó el archivo sin sobrescribir. Puedes restablecerla desde el gestor de Memoria.";
        }
    }
    internal void Queue(MemoryData snapshot)
    {
        lock(sync)
        {
            if(blocked) return;
            pending=snapshot;
            if(worker.IsCompleted) worker=Task.Run(WriteLoop);
        }
    }
    private void WriteLoop()
    {
        while(true)
        {
            MemoryData? snapshot;
            lock(sync)
            {
                snapshot=pending; pending=null;
                if(snapshot is null) worker=Task.CompletedTask;
            }
            if(snapshot is null) { Completed?.Invoke(); return; }
            try
            {
                if(path is not null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    byte[] data=ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(snapshot),null,DataProtectionScope.CurrentUser);
                    string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                    try { File.WriteAllBytes(temporary,data); File.Move(temporary,path,true); }
                    finally { if(File.Exists(temporary)) File.Delete(temporary); }
                }
                lock(sync) error="";
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
            { lock(sync) error="Windows no pudo guardar la memoria. Los mensajes de esta sesión siguen disponibles mientras BAEL esté abierto."; }
            Completed?.Invoke();
        }
    }
    internal void Flush()
    {
        Task task; lock(sync) task=worker;
        task.GetAwaiter().GetResult();
    }
    internal bool Reset()
    {
        Flush();
        try
        {
            if(path is not null && File.Exists(path)) File.Delete(path);
            lock(sync) { blocked=false; error=""; pending=null; }
            return true;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException)
        { lock(sync) error="No se pudo borrar el archivo de memoria. Windows no permitió el cambio."; return false; }
    }
    internal static void Verify()
    {
        string folder=Path.Combine(Path.GetTempPath(),"BAEL-memory-"+Guid.NewGuid().ToString("N"));
        string file=Path.Combine(folder,"memory.dat");
        try
        {
            var state=new MemoryData();
            state.Messages[ChatMode.Free]=[new("Mensaje de prueba único",true,DateTimeOffset.Now)];
            state.Context[ChatMode.Free]=[new("user","Mensaje"),new("model","Respuesta")];
            state.Notes.Add(new("one",ChatMode.Free,"Me llamo Yisus"));
            state.Notes.Add(new("two",ChatMode.English,"Practico verbos",false));
            var journal=new MemoryJournal(file);
            for(int i=0;i<20;i++) { state.Messages[ChatMode.Free].Add(new("Turno "+i,false,DateTimeOffset.Now)); journal.Queue(state.Snapshot()); }
            journal.Flush();
            var loaded=new MemoryJournal(file);
            if(loaded.Blocked || loaded.Loaded.Messages[ChatMode.Free].Count!=21 || loaded.Loaded.Context[ChatMode.Free].Count!=2
                || !loaded.Loaded.ContextFor(ChatMode.Free).Contains("Yisus") || loaded.Loaded.ContextFor(ChatMode.English)!="[]")
                throw new InvalidOperationException("La memoria perdió mensajes, contexto o separación por modo.");
            if(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(file)).Contains("Mensaje de prueba único"))
                throw new InvalidOperationException("La memoria se guardó sin protección.");
            loaded.Queue(loaded.Loaded.Snapshot(false)); loaded.Flush();
            var noHistory=new MemoryJournal(file);
            if(noHistory.Loaded.Messages.Count!=0 || noHistory.Loaded.Notes.Count!=2) throw new InvalidOperationException("Desactivar historial borró recuerdos o conservó mensajes.");
            File.WriteAllText(file,"archivo dañado");
            var corrupt=new MemoryJournal(file); corrupt.Queue(state.Snapshot()); corrupt.Flush();
            if(!corrupt.Blocked || File.ReadAllText(file)!="archivo dañado" || !corrupt.Reset())
                throw new InvalidOperationException("Una memoria ilegible fue sobrescrita.");
            corrupt.Queue(state.Snapshot()); corrupt.Flush();
            if(new MemoryJournal(file).Blocked) throw new InvalidOperationException("No se restableció la memoria.");
            state.Messages[ChatMode.Free]=Enumerable.Range(0,150).Select(i=>new SavedMessage(i.ToString(),true,DateTimeOffset.Now)).ToList();
            if(state.Snapshot().Messages[ChatMode.Free].Count!=100) throw new InvalidOperationException("El historial no respetó su límite.");
        }
        finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
}
