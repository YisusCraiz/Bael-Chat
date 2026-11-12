using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Bael;

public partial class MainWindow : Window
{
    private ChatMode mode = ChatMode.Student;
    private long firstClick;
    private int clickCount;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenChat(object sender, RoutedEventArgs e)
    {
        Chat.Visibility = Visibility.Visible;

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
