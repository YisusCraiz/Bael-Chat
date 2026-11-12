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
    private double nextWalkPosition = 64;
    private ChatMode mode = ChatMode.Student;
    private long firstClick;
    private int clickCount;

    public MainWindow()
    {
        InitializeComponent();
        MascotButton.MouseEnter += (_, _) => StopWalking();
        mascotTimer.Tick += AnimateMascot;
        mascotTimer.Start();
        Closed += (_, _) => mascotTimer.Stop();
    }

    private void OpenChat(object sender, RoutedEventArgs e)
    {
        Chat.Visibility = Visibility.Visible;
        StopWalking();
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
        string reply = $"BAEL ({GetModeName(mode)}): {Responses.For(mode, message)}";
        Conversation.Items.Add(reply);
        Conversation.ScrollIntoView(reply);
        Message.Clear();
        Message.Focus();
    }

    private static string GetModeName(ChatMode option) => option switch
    {
        ChatMode.Tarot => "Tarot",
        ChatMode.Comedy => "Comedia",
        _ => "Estudiante"
    };
}
