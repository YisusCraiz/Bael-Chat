using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;

namespace Bael;

public partial class SettingsWindow : Window
{
    internal BaelSettings Settings { get; private set; }

    internal SettingsWindow(BaelSettings settings, bool unreadable)
    {
        InitializeComponent();
        Settings = settings;
        PinWindow.IsChecked = settings.Topmost;
        AllowWalking.IsChecked = settings.Walk;
        Volume.Value = settings.Volume * 100;
        OnlineChat.IsChecked = settings.OnlineChat;
        GeminiKey.Password = settings.GeminiKey;
        GeminiModel.Text = settings.GeminiModel;
        VoiceKey.Password = settings.VoiceKey;
        VoiceId.Text = settings.VoiceId;
        if (unreadable)
            Status.Text = "No se pudo leer la configuración anterior. Guardar creará una nueva.";
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        string model = GeminiModel.Text.Trim();
        if (model.StartsWith("models/", StringComparison.Ordinal))
            model = model["models/".Length..];

        if (model.Length > 0 && !Regex.IsMatch(model, "^[a-zA-Z0-9._-]+$"))
        {
            Status.Text = "Escribe un ID de modelo válido, por ejemplo gemini-2.5-flash.";
            return;
        }

        var updated = new BaelSettings
        {
            Topmost = PinWindow.IsChecked == true,
            Walk = AllowWalking.IsChecked == true,
            Volume = Volume.Value / 100,
            OnlineChat = OnlineChat.IsChecked == true,
            GeminiKey = GeminiKey.Password.Trim(),
            GeminiModel = model.Length == 0 ? new BaelSettings().GeminiModel : model,
            VoiceKey = VoiceKey.Password.Trim(),
            VoiceId = VoiceId.Text.Trim()
        };

        if (updated.OnlineChat && updated.EffectiveGeminiKey.Length == 0)
        {
            Status.Text = "Agrega una clave de Gemini para activar el chat en línea.";
            return;
        }

        try
        {
            updated.Save();
            Settings = updated;
            DialogResult = true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            Status.Text = "Windows no pudo guardar tus ajustes. Revisa el acceso a tu perfil.";
        }
    }
}
