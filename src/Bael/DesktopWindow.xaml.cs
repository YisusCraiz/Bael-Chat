using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Bael;

public partial class DesktopWindow : Window
{
    private readonly MainWindow chat = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly Dictionary<string, BitmapImage[]> frames = new();
    private readonly Queue<long> messages = new();
    private BaelSettings settings;
    private string outfit = "boy";
    private string? transition;
    private int frame;
    private long lastActivity = Environment.TickCount64;
    private int direction = 1;
    private Point? dragStart;
    private bool dragged;
    private bool closing;
    private bool settingsOpen;

    public DesktopWindow(bool testMode = false)
    {
        InitializeComponent();
        settings = chat.CurrentSettings;
        Topmost = settings.Topmost;
        foreach (string name in new[] { "boy", "headless", "unicorn", "magic" })
        {
            frames[name] = Enumerable.Range(0, 4).Select(index =>
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Sprites", "desktop", $"{name}-{index}.png");
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path);
                image.EndInit();
                image.Freeze();
                return image;
            }).ToArray();
        }

        SetOutfit("boy");
        Loaded += (_, _) =>
        {
            Left = SystemParameters.WorkArea.Right - Width - 30;
            Top = SystemParameters.WorkArea.Bottom - Height;
            timer.Start();
        };
        timer.Tick += Tick;
        chat.UserSpoke += OnMessage;
        chat.SettingsChanged += updated =>
        {
            settings = updated;
            Topmost = settings.Topmost;
        };
        chat.Closing += HideChat;
        Closed += (_, _) =>
        {
            closing = true;
            timer.Stop();
            chat.Close();
            if (!testMode)
                Application.Current.Shutdown();
        };
    }

    internal void VerifyCompanion()
    {
        if (!AllowsTransparency || WindowStyle != WindowStyle.None || frames.Count != 4)
            throw new InvalidOperationException("La mascota no está preparada para el escritorio.");

        outfit = "unicorn";
        Interact();
        if (outfit != "headless")
            throw new InvalidOperationException("El unicornio no descubrió la cabeza.");

        for (int index = 0; index < Math.Clamp(settings.MessagesToUndress, 2, 10); index++)
            OnMessage();

        if (outfit != "boy")
            throw new InvalidOperationException("La conversación no retiró el disfraz.");

        foreach (var sequence in frames.Values)
            if (sequence.Length != 4 || sequence.Any(image => image.PixelWidth == 0))
                throw new InvalidOperationException("Faltan fotogramas.");
    }

    private void HideChat(object? sender, CancelEventArgs e)
    {
        if (closing)
            return;
        e.Cancel = true;
        chat.Hide();
    }

    private void SetOutfit(string name)
    {
        outfit = name;
        frame = 0;
        Character.Source = frames[name][0];
    }

    private void Interact()
    {
        lastActivity = Environment.TickCount64;
        transition = null;
        if (outfit == "unicorn")
        {
            SetOutfit("headless");
            transition = "head-off";
        }
    }

    private void OnMessage()
    {
        Interact();
        long now = Environment.TickCount64;
        messages.Enqueue(now);
        while (messages.Count > 0 && now - messages.Peek() > 60000)
            messages.Dequeue();
        if (outfit != "boy" && messages.Count >= Math.Clamp(settings.MessagesToUndress, 2, 10))
        {
            SetOutfit("boy");
            transition = "undress";
        }
    }

    private void Tick(object? sender, EventArgs e)
    {
        long now = Environment.TickCount64;
        long idleDelay = Math.Clamp(settings.IdleSeconds, 10, 300) * 1000L;
        bool paused = IsMouseOver || dragStart is not null || settingsOpen || chat.IsVisible;
        if (paused)
            lastActivity = now;

        if (!paused && now - lastActivity >= idleDelay && outfit != "unicorn" && transition is null)
        {
            transition = "dress";
            frame = 0;
            messages.Clear();
        }

        if (transition is not null)
        {
            int index = frame++ / 3;
            if (index >= 4)
            {
                string target = transition == "dress" ? "unicorn" : outfit;
                transition = null;
                SetOutfit(target);
            }
            else
            {
                Character.Source = transition switch
                {
                    "dress" => frames["magic"][index],
                    "head-off" => index switch
                    {
                        0 => frames["unicorn"][0],
                        1 => frames["magic"][2],
                        _ => frames["headless"][index - 2]
                    },
                    _ => index switch
                    {
                        0 => frames["headless"][0],
                        1 => frames["magic"][1],
                        2 => frames["magic"][0],
                        _ => frames["boy"][0]
                    }
                };
            }
            return;
        }

        bool walking = settings.Walk && !paused && outfit == "unicorn";
        int pose = walking ? 2 + (frame / 3 % 2) : (frame % 45 == 44 ? 1 : 0);
        Character.Source = frames[outfit][pose];
        frame = (frame + 1) % 900;
        if (walking)
        {
            Rect area = SystemParameters.WorkArea;
            double next = Left + direction * 3;
            if (next < area.Left || next > area.Right - Width)
                direction = -direction;
            Left = Math.Clamp(Left + direction * 3, area.Left, Math.Max(area.Left, area.Right - Width));
            Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
        }
    }

    private void RevealControls(object sender, MouseEventArgs e) => Controls.Visibility = Visibility.Visible;
    private void HideControls(object sender, MouseEventArgs e) => Controls.Visibility = Visibility.Collapsed;

    private void BeginDrag(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(this);
        dragged = false;
        Character.CaptureMouse();
    }

    private void MoveDrag(object sender, MouseEventArgs e)
    {
        if (dragStart is not Point start || e.LeftButton != MouseButtonState.Pressed)
            return;
        Point position = e.GetPosition(this);
        Vector delta = position - start;
        if (!dragged && delta.Length < 5)
            return;
        dragged = true;
        Rect area = SystemParameters.WorkArea;
        Left = Math.Clamp(Left + delta.X, area.Left, Math.Max(area.Left, area.Right - Width));
        Top = Math.Clamp(Top + delta.Y, area.Top, Math.Max(area.Top, area.Bottom - Height));
    }

    private void EndDrag(object sender, MouseButtonEventArgs e)
    {
        Character.ReleaseMouseCapture();
        dragStart = null;
        if (!dragged)
            ShowChat(sender, e);
    }

    private void ShowChat(object sender, RoutedEventArgs e)
    {
        Interact();
        chat.OpenConversation();
        if (!chat.IsVisible)
        {
            chat.Left = Math.Clamp(Left - chat.Width / 2, SystemParameters.WorkArea.Left,
                Math.Max(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Right - chat.Width));
            chat.Top = Math.Max(SystemParameters.WorkArea.Top, Top - 420);
            chat.Show();
        }
        chat.Activate();
    }

    private void ShowSettings(object sender, RoutedEventArgs e)
    {
        Interact();
        settingsOpen = true;
        try
        {
            chat.EditSettings(this);
        }
        finally
        {
            settingsOpen = false;
        }
    }

    private void Exit(object sender, RoutedEventArgs e) => Close();
}
