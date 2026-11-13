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
using System.Windows.Threading;

namespace Bael;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer mascotTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly MascotSprites sprites = new();
    private readonly DispatcherTimer spriteTimer = new();
    private ImageSource[] activeFrames = [];
    private int frameIndex;
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
    private double nextWalkPosition = 64;
    private ChatMode mode = ChatMode.Student;
    private long firstClick;
    private int clickCount;

    public MainWindow()
    {
        InitializeComponent();
        settings = BaelSettings.Load(out settingsUnreadable);
        ApplySettings();
        PlaySprites(sprites.Idle, 180);
        MascotButton.MouseEnter += (_, _) =>
        {
            if (activeFrames == sprites.Walk)
                PlaySprites(sprites.Idle, 180);
            StopWalking();
        };
        spriteTimer.Tick += AdvanceSprite;
        mascotTimer.Tick += AnimateMascot;
        mascotTimer.Start();
        Closed += (_, _) =>
        {
            isClosed = true;
            chatLifetime.Cancel();
            chatLifetime.Dispose();
            mascotTimer.Stop();
            spriteTimer.Stop();
            voiceRequest?.Cancel();
            ReleaseAudio();
        };
    }

    internal void VerifyStartup()
    {
        var click = new RoutedEventArgs(Button.ClickEvent);
        OpenChat(MascotButton, click);
        if (Chat.Visibility != Visibility.Visible || SettingsGear.Visibility != Visibility.Visible)
            throw new InvalidOperationException("El chat no abrió.");

        foreach (ChatMode profile in new[] { ChatMode.Tarot, ChatMode.Comedy })
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

        HideChat(MascotButton, click);
        if (SettingsGear.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("El engrane quedó visible al ocultar el chat.");
        OpenChat(MascotButton, click);
        if (Chat.Visibility != Visibility.Visible || Conversation.Items.Count != 4)
            throw new InvalidOperationException("El chat perdió la conversación al volver a abrirlo.");
    }

    private void OpenChat(object sender, RoutedEventArgs e)
    {
        Chat.Visibility = Visibility.Visible;
        StopWalking();
        if (SettingsGear.Visibility != Visibility.Visible)
        {
            SettingsGear.Visibility = Visibility.Visible;
            GearOffset.X = MascotPosition.X;
            GearOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
            {
                From = -12,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(180))
            });
        }
        PlaySprites(sprites.React, 100);
        Blink();

        long now = Environment.TickCount64;
        if (clickCount == 0 || now - firstClick > 1000)
        {
            firstClick = now;
            clickCount = 0;
        }

        if (++clickCount == 3)
        {
            clickCount = 0;
            OpenModes(sender, e);
        }
    }

    private void AnimateMascot(object? sender, EventArgs e)
    {
        Blink();
        if (!settings.Walk || Chat.Visibility == Visibility.Visible || MascotButton.IsMouseOver)
            return;

        PlaySprites(sprites.Walk, Math.Max(1, 960.0 / Math.Max(1, sprites.Walk.Length)));
        var walkDuration = new Duration(TimeSpan.FromMilliseconds(960));
        double destination = nextWalkPosition;
        nextWalkPosition = -nextWalkPosition;

        double start = MascotPosition.X;
        MascotPosition.X = destination;
        MascotPosition.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
        {
            From = start,
            To = destination,
            Duration = walkDuration,
            FillBehavior = FillBehavior.Stop
        });

        LeftFoot.BeginAnimation(TranslateTransform.YProperty, CreateSteps(-4));
        RightFoot.BeginAnimation(TranslateTransform.YProperty, CreateSteps(4));
    }

    private void PlaySprites(ImageSource[] frames, double frameMilliseconds)
    {
        spriteTimer.Stop();
        activeFrames = frames;
        frameIndex = 0;

        ImageSource? firstFrame = frames.FirstOrDefault();
        if (firstFrame is null && frames != sprites.Idle)
            firstFrame = sprites.Idle.FirstOrDefault();

        MascotFrame.Source = firstFrame;
        MascotFrame.Visibility = firstFrame is null ? Visibility.Collapsed : Visibility.Visible;
        DefaultMascot.Visibility = firstFrame is null ? Visibility.Visible : Visibility.Collapsed;

        if (frames.Length > 1 || (frames.Length == 1 && frames != sprites.Idle))
        {
            spriteTimer.Interval = TimeSpan.FromMilliseconds(frameMilliseconds);
            spriteTimer.Start();
        }
        else if (frames.Length == 0 && frames != sprites.Idle)
        {
            PlaySprites(sprites.Idle, 180);
        }
    }

    private void AdvanceSprite(object? sender, EventArgs e)
    {
        if (++frameIndex == activeFrames.Length)
        {
            if (activeFrames != sprites.Idle)
            {
                PlaySprites(sprites.Idle, 180);
                return;
            }

            frameIndex = 0;
        }

        MascotFrame.Source = activeFrames[frameIndex];
    }

    private static DoubleAnimation CreateSteps(double stride) => new()
    {
        From = 0,
        To = stride,
        Duration = new Duration(TimeSpan.FromMilliseconds(120)),
        AutoReverse = true,
        RepeatBehavior = new RepeatBehavior(4),
        FillBehavior = FillBehavior.Stop
    };

    private void StopWalking()
    {
        double position = MascotPosition.X;
        MascotPosition.BeginAnimation(TranslateTransform.XProperty, null);
        MascotPosition.X = position;
        LeftFoot.BeginAnimation(TranslateTransform.YProperty, null);
        RightFoot.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private void Blink()
    {
        EyesScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
        {
            To = 0.2,
            Duration = new Duration(TimeSpan.FromMilliseconds(100)),
            AutoReverse = true,
            FillBehavior = FillBehavior.Stop
        });
    }

    private void HideChat(object sender, RoutedEventArgs e)
    {
        Chat.Visibility = Visibility.Collapsed;
        SettingsGear.Visibility = Visibility.Collapsed;
        clickCount = 0;
        MascotButton.Focus();
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
                mode = option;
                ModeLabel.Text = $"Modo {GetModeName(mode).ToLowerInvariant()}";
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
        clickCount = 0;
        var dialog = new SettingsWindow(settings, settingsUnreadable) { Owner = this, Topmost = Topmost };
        dialog.LoadAudioDevices();
        if (dialog.ShowDialog() == true)
        {
            settings = dialog.Settings;
            settingsUnreadable = false;
            ApplySettings();
        }
    }

    private async void SendMessage(object sender, RoutedEventArgs e)
    {
        string message = Message.Text.Trim();
        if (message.Length == 0 || !SendButton.IsEnabled)
            return;

        ChatMode profile = mode;
        BaelSettings current = settings;
        AddUserMessage(message);
        Message.Clear();
        Message.Focus();
        ChatStatus.Text = "";

        string reply = Responses.For(profile, message);
        if (!current.OnlineChat || profile == ChatMode.Student)
        {
            ShowReply(profile, reply);
            return;
        }

        SendButton.IsEnabled = false;
        ChatStatus.Text = "Consultando Gemini…";
        try
        {
            string? onlineReply = await GeminiChat.ReplyAsync(current, profile, message, chatLifetime.Token);
            if (isClosed)
                return;

            if (string.IsNullOrWhiteSpace(onlineReply))
                ChatStatus.Text = "Gemini no devolvió texto. Se mostró una respuesta local.";
            else
            {
                reply = onlineReply;
                ChatStatus.Text = "";
            }
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException
                                      or FormatException or JsonException or InvalidOperationException)
        {
            if (isClosed)
                return;

            ChatStatus.Text = "Gemini no respondió. Revisa la clave, el modelo o la conexión. Se mostró una respuesta local.";
        }
        finally
        {
            if (!isClosed)
                SendButton.IsEnabled = true;
        }

        ShowReply(profile, reply);
    }

    private void AddUserMessage(string message)
    {
        string line = $"Tú: {message}";
        Conversation.Items.Add(line);
        Conversation.ScrollIntoView(line);
    }

    private void ShowReply(ChatMode profile, string reply)
    {
        latestReply = reply;
        ReadAloudButton.IsEnabled = true;
        string line = $"BAEL ({GetModeName(profile)}): {reply}";
        Conversation.Items.Add(line);
        Conversation.ScrollIntoView(line);
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

    private static string GetModeName(ChatMode option) => option switch
    {
        ChatMode.Tarot => "Tarot",
        ChatMode.Comedy => "Comedia",
        _ => "Estudiante"
    };
}
