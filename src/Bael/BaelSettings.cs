using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Bael;

internal sealed record BaelSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BAEL", "settings.dat");

    public bool SaveConversations { get; init; } = true;
    public bool UseConversationContext { get; init; } = true;
    public bool UsePinnedMemories { get; init; } = true;
    public string UiTheme { get; init; } = "Claro";
    public int SpeechPauseMilliseconds { get; init; } = 900;
    public double SpeechThreshold { get; init; } = 0.004;
    public double MascotScale { get; init; } = 1;
    public string EnlargeShortcut { get; init; } = "Ctrl+Alt+Up";
    public string ShrinkShortcut { get; init; } = "Ctrl+Alt+Down";
    public bool SpontaneousChat { get; init; } = true;
    public int SpontaneousSeconds { get; init; } = 180;
    public bool SpontaneousVoice { get; init; }
    public double ChatTransparency { get; init; } = 0.35;
    public string TalkShortcut { get; init; } = "Ctrl+Alt+V";
    public string SixSevenShortcut { get; init; } = "Ctrl+Alt+D6";
    public string DanceShortcut { get; init; } = "Ctrl+Alt+B";
    public bool Topmost { get; init; } = true;
    public int IdleSeconds { get; init; } = 30;
    public int MessagesToUndress { get; init; } = 4;
    public bool DanceWithAudio { get; init; }
    public double DanceThreshold { get; init; } = 0.03;
    public bool Walk { get; init; } = true;
    public double Volume { get; init; } = 0.8;
    public List<PdfEntry> Documents { get; init; } = [];
    public Dictionary<ChatMode, DocumentRule>? DocumentRules { get; init; }
    public DocumentRule GetDocumentRule(ChatMode mode) =>
        DocumentRules is not null && DocumentRules.TryGetValue(mode, out var rule)
            ? rule : new DocumentRule { Limited = mode == ChatMode.Student };

    public bool OnlineChat { get; init; }
    public bool AutoFallback { get; init; } = true;
    public List<AiProfile>? Profiles { get; init; }
    public List<AiProfile> GetProfiles() => Profiles ?? [new AiProfile
    {
        Provider = "Gemini", Model = GeminiModel, Key = GeminiKey
    }];

    public Dictionary<ChatMode, ModePersonality>? Personalities { get; init; }
    public ModePersonality GetPersonality(ChatMode mode) =>
        Personalities is not null && Personalities.TryGetValue(mode, out var personality)
            ? personality : ModePersonality.Default(mode);

    public string GeminiKey { get; init; } = "";
    public string GeminiModel { get; init; } = "gemini-3.8-flash";
    public string SpeechModel { get; init; } = "scribe_v2";
    public bool SpeakTextReplies { get; init; } = true;
    public bool SpeakVoiceReplies { get; init; } = true;
    public string VoiceModel { get; init; } = "eleven_multilingual_v2";
    public string VoiceKey { get; init; } = "";
    public string InputDeviceId { get; init; } = "";
    public string OutputDeviceId { get; init; } = "";
    public string VoiceId { get; init; } = "";

    internal string EffectiveGeminiKey => Resolve(GeminiKey, "BAEL_GEMINI_API_KEY");
    internal string EffectiveVoiceKey => Resolve(VoiceKey, "BAEL_ELEVENLABS_API_KEY");
    internal string EffectiveVoiceId => Resolve(VoiceId, "BAEL_ELEVENLABS_VOICE_ID");

    private static string Resolve(string local, string variable) =>
        string.IsNullOrWhiteSpace(local) ? Environment.GetEnvironmentVariable(variable)?.Trim() ?? "" : local;

    public static BaelSettings Load(out bool unreadable)
    {
        unreadable = false;
        try
        {
            if (!File.Exists(FilePath))
                return new();

            byte[] data = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<BaelSettings>(data) ?? new();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                     or CryptographicException or JsonException)
        {
            unreadable = true;
            return new();
        }
    }

    public void Save()
    {
        byte[] data = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(this), null, DataProtectionScope.CurrentUser);
        string directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
