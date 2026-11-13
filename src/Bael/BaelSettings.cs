using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Bael;

internal sealed record BaelSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BAEL", "settings.dat");

    public bool Topmost { get; init; } = true;
    public bool Walk { get; init; } = true;
    public double Volume { get; init; } = 0.8;
    public bool OnlineChat { get; init; }
    public string GeminiKey { get; init; } = "";
    public string GeminiModel { get; init; } = "gemini-2.5-flash";
    public string VoiceKey { get; init; } = "";
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
