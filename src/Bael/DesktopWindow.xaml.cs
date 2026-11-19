using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using System.Windows.Media.Animation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private ChatMode selectedMode = ChatMode.Student;
    private long actionUntil;
    private string outfit = "boy";
    private string? transition;
    private int frame;
    private long lastActivity = Environment.TickCount64;
    private readonly WanderMotion motion = new();
    private readonly DispatcherTimer movementTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer bubbleTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private long movementTimestamp = Stopwatch.GetTimestamp();
    private string bubbleReply = "";
    private int bubbleCharacters;
    private long bubbleExpires;
    private Point? dragStart;
    private bool dragged;
    private bool closing;
    private bool settingsOpen;

    public DesktopWindow(bool testMode = false)
    {
        InitializeComponent();
        settings = chat.CurrentSettings;
        Topmost = settings.Topmost;
        foreach (string name in new[] { "boy", "headless", "unicorn", "magic", "tarot", "comedy", "system", "math", "english" })
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

        BuildWheel();
        SetOutfit("boy");
        Loaded += (_, _) =>
        {
            Left = SystemParameters.WorkArea.Right - Width - 30;
            Top = SystemParameters.WorkArea.Bottom - Height;
            timer.Start();
            movementTimestamp = Stopwatch.GetTimestamp();
            movementTimer.Start();
        };
        timer.Tick += Tick;
        movementTimer.Tick += MoveFreely;
        bubbleTimer.Tick += WriteBubble;
        chat.ReplyReady += ShowBubble;
        chat.UserSpoke += OnMessage;
        chat.ModeChanged += selected =>
        {
            AnswerBubble.IsOpen = false;
            bubbleTimer.Stop();
            selectedMode = selected;
            transition = null;
            messages.Clear();
            lastActivity = Environment.TickCount64;
            actionUntil = lastActivity + 1800;
            SetOutfit(Responses.Sprite(selected));
        };
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
            movementTimer.Stop();
            bubbleTimer.Stop();
            AnswerBubble.IsOpen = false;
            ModeWheel.IsOpen = false;
            chat.Close();
            if (!testMode)
                Application.Current.Shutdown();
        };
    }

    internal void VerifyCompanion()
    {
        if (!AllowsTransparency || WindowStyle != WindowStyle.None || frames.Count != 9)
            throw new InvalidOperationException("La mascota no está preparada para el escritorio.");

        outfit = "unicorn";
        Interact();
        if (outfit != "headless")
            throw new InvalidOperationException("El unicornio no descubrió la cabeza.");

        for (int index = 0; index < Math.Clamp(settings.MessagesToUndress, 2, 10); index++)
            OnMessage();

        if (outfit != "boy")
            throw new InvalidOperationException("La conversación no retiró el disfraz.");

        WanderMotion.Verify();
        foreach (ChatMode mode in Enum.GetValues<ChatMode>())
        {
            ((Button)WheelCanvas.Children[Array.IndexOf(Enum.GetValues<ChatMode>(), mode)])
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (outfit != Responses.Sprite(mode) || selectedMode != mode || chat.IsVisible)
                throw new InvalidOperationException("La selección del modo no cambió el sprite.");
            if (string.IsNullOrWhiteSpace(Responses.For(mode, "prueba")))
                throw new InvalidOperationException("El modo no tiene respuesta local.");
        }
        chat.SelectMode(ChatMode.Student);
        ShowBubble(ChatMode.Student, new string('a', 400));
        WriteBubble(this, EventArgs.Empty);
        if (bubbleReply.Length > 180 || BubbleText.Text.Length == 0 || !AnswerBubble.IsOpen)
            throw new InvalidOperationException("La burbuja no mostró el fragmento de respuesta.");
        AnswerBubble.IsOpen = false;
        bubbleTimer.Stop();
        if (WheelCanvas.Children.Count != 8)
            throw new InvalidOperationException("Faltan burbujas de la rueda.");

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
        Character.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(220)));
    }

    private void Interact()
    {
        lastActivity = Environment.TickCount64;
        transition = null;
        if (selectedMode == ChatMode.Student && outfit == "unicorn")
        {
            SetOutfit("headless");
            transition = "head-off";
        }
    }

    private void OnMessage()
    {
        Interact();
        long now = Environment.TickCount64;
        actionUntil = now + 2400;
        if (selectedMode != ChatMode.Student)
            return;
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
        bool paused = IsMouseOver || dragStart is not null || settingsOpen || chat.IsVisible || ModeWheel.IsOpen || AnswerBubble.IsOpen;
        if (paused)
            lastActivity = now;

        if (selectedMode == ChatMode.Student && !paused && now - lastActivity >= idleDelay && outfit != "unicorn" && transition is null)
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

        bool walking = motion.IsMoving;
        int pose = walking || (selectedMode != ChatMode.Student && now < actionUntil)
            ? 2 + (frame / 3 % 2) : (frame % 45 >= 42 ? 1 : 0);
        Character.Source = frames[outfit][pose];
        frame = (frame + 1) % 900;
    }

    private void MoveFreely(object? sender, EventArgs e)
    {
        double seconds = Stopwatch.GetElapsedTime(movementTimestamp).TotalSeconds;
        movementTimestamp = Stopwatch.GetTimestamp();
        bool paused = !settings.Walk || IsMouseOver || dragStart is not null || settingsOpen
            || chat.IsVisible || ModeWheel.IsOpen || AnswerBubble.IsOpen || transition is not null;
        if (paused)
            motion.Stop();
        else
        {
            Rect area = SystemParameters.WorkArea;
            var bounds = new Rect(area.Left, area.Top, Math.Max(0, area.Width - Width), Math.Max(0, area.Height - Height));
            Point next = motion.Advance(new Point(Left, Top), bounds, seconds);
            Left = next.X;
            Top = next.Y;
        }

        double phase = Environment.TickCount64 / (motion.IsMoving ? 100.0 : 600.0);
        BodyBob.Y = Math.Sin(phase) * (motion.IsMoving ? 2.4 : 0.8);
        BodyTilt.Angle = motion.IsMoving ? Math.Sin(phase) * 1.8 : 0;
        if (Math.Abs(motion.HorizontalSpeed) > 5)
            Facing.ScaleX = motion.HorizontalSpeed < 0 ? -1 : 1;
    }

    private void ShowBubble(ChatMode mode, string reply)
    {
        if (closing) return;
        bubbleReply = reply.Length > 180 ? reply[..177] + "…" : reply;
        bubbleCharacters = 0;
        BubbleMode.Text = $"BAEL · {Responses.Name(mode)}";
        BubbleText.Text = "";
        bubbleExpires = Environment.TickCount64 + 14000;
        actionUntil = Environment.TickCount64 + 2400;
        AnswerBubble.IsOpen = true;
        bubbleTimer.Start();
    }

    private void WriteBubble(object? sender, EventArgs e)
    {
        if (!AnswerBubble.IsOpen || Environment.TickCount64 > bubbleExpires)
        {
            AnswerBubble.IsOpen = false;
            bubbleTimer.Stop();
            return;
        }
        bubbleCharacters = Math.Min(bubbleCharacters + 3, bubbleReply.Length);
        BubbleText.Text = bubbleReply[..bubbleCharacters];
    }

    private void BuildWheel()
    {
        ChatMode[] modes = Enum.GetValues<ChatMode>();
        for (int index = 0; index < 8; index++)
        {
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (index < modes.Length)
            {
                ChatMode option = modes[index];
                content.Children.Add(new Image { Source = frames[Responses.Sprite(option)][0], Height = 40 });
                content.Children.Add(new TextBlock
                {
                    Text = Responses.Name(option), FontSize = 10,
                    TextAlignment = TextAlignment.Center
                });
            }
            else
            {
                content.Children.Add(new TextBlock
                {
                    Text = index == 6 ? "⚙" : "×", FontSize = 23,
                    TextAlignment = TextAlignment.Center
                });
                content.Children.Add(new TextBlock
                {
                    Text = index == 6 ? "Ajustes" : "Salir", FontSize = 10,
                    TextAlignment = TextAlignment.Center
                });
            }

            var circle = new FrameworkElementFactory(typeof(Border));
            circle.SetValue(Border.CornerRadiusProperty, new CornerRadius(40));
            circle.SetValue(Border.BackgroundProperty, Brushes.White);
            circle.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(118, 107, 209)));
            circle.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            circle.AppendChild(presenter);
            var button = new Button
            {
                Width = 78, Height = 78, Content = content,
                Cursor = Cursors.Hand,
                Template = new ControlTemplate(typeof(Button)) { VisualTree = circle }
            };
            int slot = index;
            string label = slot < modes.Length ? Responses.Name(modes[slot]) : slot == 6 ? "Ajustes" : "Salir";
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.ToolTip = label;
            button.Click += (_, args) =>
            {
                ModeWheel.IsOpen = false;
                if (slot < modes.Length)
                {
                    chat.SelectMode(modes[slot]);
                    chat.Hide();
                }
                else if (slot == 6)
                    ShowSettings(button, args);
                else
                    Close();
            };
            double angle = (-90 + index * 45) * Math.PI / 180;
            Canvas.SetLeft(button, 146 + Math.Cos(angle) * 139);
            Canvas.SetTop(button, 146 + Math.Sin(angle) * 139);
            WheelCanvas.Children.Add(button);
        }
    }

    private void OpenWheel(object sender, MouseButtonEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            return;
        ToggleWheel();
        e.Handled = true;
    }

    private void ToggleWheel()
    {
        lastActivity = Environment.TickCount64;
        ModeWheel.IsOpen = !ModeWheel.IsOpen;
        if (ModeWheel.IsOpen)
            ((Button)WheelCanvas.Children[0]).Focus();
    }

    private void WheelKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ModeWheel.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.F10 && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            ToggleWheel();
            e.Handled = true;
        }
    }

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
        AnswerBubble.IsOpen = false;
        bubbleTimer.Stop();
        Interact();
        chat.OpenConversation();
        if (!chat.IsVisible)
        {
            chat.Left = Math.Clamp(Left - chat.Width / 2, SystemParameters.WorkArea.Left,
                Math.Max(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Right - chat.Width));
            chat.Top = Math.Clamp(Top - 420, SystemParameters.WorkArea.Top,
                Math.Max(SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom - chat.Height));
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
