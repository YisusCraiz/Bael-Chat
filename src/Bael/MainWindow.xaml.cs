using System.IO;
using System.Net.Http;
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
    private readonly MediaPlayer voicePlayer = new();
    private CancellationTokenSource? voiceRequest;
    private string? voiceFile;
    private string? latestReply;
    private bool isClosed;
    private double nextWalkPosition = 64;
    private ChatMode mode = ChatMode.Student;
    private long firstClick;
    private int clickCount;

    public MainWindow()
    {
        InitializeComponent();
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
        voicePlayer.MediaEnded += (_, _) => EndPlayback("");
        voicePlayer.MediaFailed += (_, _) => EndPlayback("Windows no pudo reproducir el audio.");
        Closed += (_, _) =>
        {
            isClosed = true;
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
        if (Chat.Visibility != Visibility.Visible)
            throw new InvalidOperationException("El chat no abrió.");

        foreach (ChatMode profile in new[] { ChatMode.Tarot, ChatMode.Comedy })
        {
            mode = profile;
            Message.Text = "Mensaje de prueba";
            int previousCount = Conversation.Items.Count;
            SendMessage(ReadAloudButton, click);
            if (Conversation.Items.Count != previousCount + 2 || string.IsNullOrWhiteSpace(latestReply))
                throw new InvalidOperationException($"El perfil {profile} no devolvió una respuesta.");
        }

        HideChat(MascotButton, click);
        OpenChat(MascotButton, click);
        if (Chat.Visibility != Visibility.Visible || Conversation.Items.Count != 4)
            throw new InvalidOperationException("El chat perdió la conversación al volver a abrirlo.");
    }

    private void OpenChat(object sender, RoutedEventArgs e)
    {
        Chat.Visibility = Visibility.Visible;
        StopWalking();
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
        if (Chat.Visibility == Visibility.Visible || MascotButton.IsMouseOver)
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

    private void SendMessage(object sender, RoutedEventArgs e)
    {
        string message = Message.Text.Trim();
        if (message.Length == 0)
        {
            Message.Focus();
            return;
        }

        Conversation.Items.Add($"Tú: {message}");
        latestReply = Responses.For(mode, message);
        ReadAloudButton.IsEnabled = true;
        string reply = $"BAEL ({GetModeName(mode)}): {latestReply}";
        Conversation.Items.Add(reply);
        Conversation.ScrollIntoView(reply);
        Message.Clear();
        Message.Focus();
    }

    private async void ReadAloud(object sender, RoutedEventArgs e)
    {
        if (voiceRequest is not null)
        {
            voiceRequest.Cancel();
            return;
        }

        if (voiceFile is not null)
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
            byte[] audio = await ElevenLabsVoice.GenerateAsync(text, request.Token);
            request.Token.ThrowIfCancellationRequested();

            voiceFile = Path.Combine(Path.GetTempPath(), $"bael-{Guid.NewGuid():N}.mp3");
            File.WriteAllBytes(voiceFile, audio);
            ReadAloudButton.Content = "Detener audio";
            VoiceStatus.Text = "Reproduciendo…";
            voicePlayer.Open(new Uri(voiceFile));
            voicePlayer.Play();
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
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
        {
            EndPlayback("Revisa la configuración de voz y el acceso a la carpeta temporal.");
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

    private void ReleaseAudio()
    {
        voicePlayer.Close();
        if (voiceFile is not string file)
            return;

        voiceFile = null;
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
            // Windows may still hold the temporary audio file.
        }
        catch (UnauthorizedAccessException)
        {
            // The temporary directory may be restricted.
        }
    }

    private static string GetModeName(ChatMode option) => option switch
    {
        ChatMode.Tarot => "Tarot",
        ChatMode.Comedy => "Comedia",
        _ => "Estudiante"
    };
}
