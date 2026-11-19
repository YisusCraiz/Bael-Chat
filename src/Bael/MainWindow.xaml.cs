using System.IO;
using System.Net.Http;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;

namespace Bael;

public partial class MainWindow : Window
{
    internal BaelSettings CurrentSettings => settings;
    internal event Action? UserSpoke;
    internal event Action<ChatMode, string>? ReplyReady;
    internal event Action<ChatMode>? ModeChanged;
    private readonly ChatRouter router = new();
    private readonly Dictionary<ChatMode, List<ChatTurn>> histories = new();

    internal void SelectMode(ChatMode selected)
    {
        mode = selected;
        ModeLabel.Text = $"Modo {Responses.Name(mode).ToLowerInvariant()}";
        ModeChanged?.Invoke(mode);
        ChatStatus.Text = settings.GetDocumentRule(mode).Limited
            ? "Solo documentos · Consulta los PDF asignados en Ajustes."
            : mode == ChatMode.System
            ? "Escribe diagnóstico para una revisión local básica."
            : mode == ChatMode.Math ? "Escribe el ejercicio y tu intento. Te guío con pistas."
            : mode == ChatMode.English ? "Pide presente, pasado, futuro o pronunciación." : "";
    }
    internal event Action<BaelSettings>? SettingsChanged;

    internal void OpenConversation() { Chat.Visibility = Visibility.Visible; Message.Focus(); }

    internal void EditSettings(Window owner)
    {
        var dialog = new SettingsWindow(settings, settingsUnreadable) { Owner = owner, Topmost = owner.Topmost };
        dialog.LoadAudioDevices();
        if (dialog.ShowDialog() == true)
        {
            settings = dialog.Settings;
            settingsUnreadable = false;
            ApplySettings();
            SettingsChanged?.Invoke(settings);
        }
    }

    private BaelSettings settings;
    private bool settingsUnreadable;
    private readonly CancellationTokenSource chatLifetime = new();
    private WasapiOut? voicePlayer;
    private MMDevice? voiceOutput;
    private Mp3FileReader? voiceReader;
    private MemoryStream? voiceStream;
    private VolumeSampleProvider? voiceVolume;
    private CancellationTokenSource? voiceRequest;
    private string? latestReply;
    private bool isClosed;
    private ChatMode mode = ChatMode.Student;

    public MainWindow()
    {
        InitializeComponent();
        settings = BaelSettings.Load(out settingsUnreadable);
        ApplySettings();
        Closed += (_, _) =>
        {
            isClosed = true;
            chatLifetime.Cancel();
            chatLifetime.Dispose();
            voiceRequest?.Cancel();
            ReleaseAudio();
        };
    }

    internal void VerifyStartup()
    {
        var click = new RoutedEventArgs(Button.ClickEvent);
        OpenConversation();
        if (Chat.Visibility != Visibility.Visible)
            throw new InvalidOperationException("El chat no abrió.");

        foreach (ChatMode profile in Enum.GetValues<ChatMode>())
        {
            mode = profile;
            const string message = "Mensaje de prueba";
            int previousCount = Conversation.Items.Count;
            AddUserMessage(message);
            ShowReply(profile, Responses.For(profile, message));
            if (Conversation.Items.Count != previousCount + 2 || string.IsNullOrWhiteSpace(latestReply))
                throw new InvalidOperationException($"El perfil {profile} no devolvió una respuesta.");
        }

        var settingsWindow = new SettingsWindow(settings, settingsUnreadable);
        settingsWindow.Close();

        HideChat(this, click);
        OpenConversation();
        if (Chat.Visibility != Visibility.Visible || Conversation.Items.Count != Enum.GetValues<ChatMode>().Length * 2)
            throw new InvalidOperationException("El chat perdió la conversación al volver a abrirlo.");
    }

    private void HideChat(object sender, RoutedEventArgs e) => Hide();

    private void MessageKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            SendMessage(sender, e);
        }
    }

    private void CopyReply(object sender, RoutedEventArgs e)
    {
        if (latestReply is null) return;
        try { Clipboard.SetText(latestReply); }
        catch (System.Runtime.InteropServices.COMException) { ChatStatus.Text = "El portapapeles está ocupado. Intenta otra vez."; }
    }

    private void OpenModes(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = Modes,
            Placement = PlacementMode.Bottom
        };

        foreach (ChatMode option in Enum.GetValues<ChatMode>())
        {
            var item = new MenuItem { Header = GetModeName(option), IsCheckable = true, IsChecked = option == mode };
            item.Click += (_, _) =>
            {
                SelectMode(option);
                Message.Focus();
            };
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    private void ApplySettings()
    {
        Topmost = settings.Topmost;
        if (voiceVolume is not null)
            voiceVolume.Volume = (float)Math.Clamp(settings.Volume, 0, 1);
    }

    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        EditSettings(this);
    }

    private async void SendMessage(object sender, RoutedEventArgs e)
    {
        string message = Message.Text.Trim();
        if (message.Length == 0 || !SendButton.IsEnabled)
            return;

        UserSpoke?.Invoke();
        ChatMode profile = mode;
        BaelSettings current = settings;
        AddUserMessage(message);
        Message.Clear();
        Message.Focus();
        ChatStatus.Text = "";

        if (current.GetDocumentRule(profile).Limited)
        {
            SendButton.IsEnabled = false;
            ChatStatus.Text = "Buscando en tus PDF…";
            try
            {
                string answer = await Task.Run(() => PdfLibrary.Answer(current, profile, message), chatLifetime.Token);
                if (!isClosed)
                {
                    ShowReply(profile, answer);
                    ChatStatus.Text = "Solo documentos · Pasajes del material seleccionado";
                }
            }
            catch (OperationCanceledException) when (isClosed) { }
            finally
            {
                if (!isClosed) SendButton.IsEnabled = true;
            }
            return;
        }

        string reply = current.GetPersonality(profile).FormatLocal(profile, Responses.For(profile, message));
        if (profile == ChatMode.System &&
            (message.Contains("diagn", StringComparison.OrdinalIgnoreCase)
             || message.Contains("examina", StringComparison.OrdinalIgnoreCase)
             || message.Contains("revisa", StringComparison.OrdinalIgnoreCase)))
        {
            ShowReply(profile, reply);
            return;
        }

        if (!histories.TryGetValue(profile, out var history))
        {
            history = new List<ChatTurn>();
            histories[profile] = history;
        }
        if (!current.OnlineChat)
        {
            ShowReply(profile, reply);
            return;
        }

        SendButton.IsEnabled = false;
        ChatStatus.Text = "Consultando las APIs activas…";
        try
        {
            ChatResult result = await router.ReplyAsync(current, profile, message, history.ToArray(), chatLifetime.Token,
                new Progress<string>(status => { if (!isClosed) ChatStatus.Text = status; }));
            if (isClosed)
                return;

            if (!string.IsNullOrWhiteSpace(result.Text))
                reply = result.Text;
            ChatStatus.Text = result.Status;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException
                                      or FormatException or JsonException or InvalidOperationException)
        {
            if (isClosed)
                return;

            ChatStatus.Text = "Las APIs no respondieron. Revisa Ajustes. Se mostró una guía local.";
        }
        finally
        {
            if (!isClosed)
                SendButton.IsEnabled = true;
        }

        history.Add(new ChatTurn("user", message));
        history.Add(new ChatTurn("model", reply));
        if (history.Count > 12)
            history.RemoveRange(0, history.Count - 12);
        ShowReply(profile, reply);
    }

    private void AddUserMessage(string message)
    {
        var line = new ConversationLine("Tú", message, true);
        Conversation.Items.Add(line);
        Conversation.ScrollIntoView(line);
    }

    private void ShowReply(ChatMode profile, string reply)
    {
        latestReply = reply;
        ReadAloudButton.IsEnabled = true;
        var line = new ConversationLine($"BAEL · {GetModeName(profile)}", reply, false);
        Conversation.Items.Add(line);
        Conversation.ScrollIntoView(line);
        ReplyReady?.Invoke(profile, reply);
    }

    private async void ReadAloud(object sender, RoutedEventArgs e)
    {
        if (voiceRequest is not null)
        {
            voiceRequest.Cancel();
            return;
        }

        if (voicePlayer is not null)
        {
            EndPlayback("");
            return;
        }

        if (latestReply is not string text)
            return;

        using var request = new CancellationTokenSource();
        voiceRequest = request;
        ReadAloudButton.Content = "Cancelar";
        VoiceStatus.Text = "Generando audio…";
        try
        {
            byte[] audio = await ElevenLabsVoice.GenerateAsync(settings, text, request.Token);
            request.Token.ThrowIfCancellationRequested();

            StartPlayback(audio);
        }
        catch (OperationCanceledException)
        {
            if (!isClosed)
                EndPlayback(request.IsCancellationRequested ? "" : "ElevenLabs tardó demasiado. Intenta de nuevo.");
        }
        catch (HttpRequestException error)
        {
            EndPlayback(error.StatusCode is { } status
                ? $"ElevenLabs devolvió el código {(int)status}. Revisa la clave, la voz y el saldo."
                : "No se pudo conectar con ElevenLabs.");
        }
        catch (InvalidOperationException error)
        {
            EndPlayback(error.Message);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException
                                      or System.Runtime.InteropServices.COMException or NAudio.MmException)
        {
            EndPlayback("No se pudo reproducir el audio. Revisa el dispositivo de salida en Ajustes.");
        }
        finally
        {
            voiceRequest = null;
        }
    }

    private void EndPlayback(string status)
    {
        ReleaseAudio();
        ReadAloudButton.Content = "Leer en voz alta";
        VoiceStatus.Text = status;
    }

    private void StartPlayback(byte[] audio)
    {
        voiceOutput = AudioDevices.Open(settings.OutputDeviceId, DataFlow.Render);
        voiceStream = new MemoryStream(audio, writable: false);
        voiceReader = new Mp3FileReader(voiceStream);
        voiceVolume = new VolumeSampleProvider(voiceReader.ToSampleProvider());
        var player = new WasapiOut(voiceOutput, AudioClientShareMode.Shared, true, 100);
        voicePlayer = player;
        player.PlaybackStopped += (_, args) => Dispatcher.InvokeAsync(() =>
        {
            if (voicePlayer == player)
                EndPlayback(args.Exception is null ? "" : "Se interrumpió la reproducción de voz.");
        });
        ApplySettings();
        player.Init(voiceVolume.ToWaveProvider());
        player.Play();
        ReadAloudButton.Content = "Detener audio";
        VoiceStatus.Text = "Reproduciendo…";
    }

    private void ReleaseAudio()
    {
        var player = voicePlayer;
        voicePlayer = null;
        player?.Dispose();
        voiceReader?.Dispose();
        voiceReader = null;
        voiceStream?.Dispose();
        voiceStream = null;
        voiceOutput?.Dispose();
        voiceOutput = null;
        voiceVolume = null;
    }

    private static string GetModeName(ChatMode option) => Responses.Name(option);
}

internal sealed record ConversationLine(string Speaker, string Text, bool IsUser)
{
    public string Background => IsUser ? "#EBE4FA" : "#F4F3F8";
    public Thickness Margin => IsUser ? new Thickness(38, 0, 0, 0) : new Thickness(0, 0, 38, 0);
}
