using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.IO;

namespace Bael;

public sealed class GameWindow : Window
{
    private readonly GameSession session = new();
    private readonly ModePersonality personality;
    private readonly TextBlock balance = new(), status = new(), result = new();
    private readonly ComboBox stake = new() { Width = 110, ItemsSource = new[] { 10, 50, 100 }, SelectedIndex = 1 };
    private readonly Button start = new() { Content = "Apostar fichas y empezar" };
    private readonly Button draw = new() { Content = "Cambiar y revelar", IsEnabled = false };
    private readonly StackPanel table = new();
    private readonly Button cupsTab = new() { Content = "Vasos" }, pokerTab = new() { Content = "Póker" };
    private readonly List<CheckBox> picks = new();
    private readonly CancellationTokenSource lifetime = new();
    private bool poker, busy;
    internal event Action<string>? Comment;

    internal GameWindow(ModePersonality personality)
    {
        this.personality = personality;
        Title = "BAEL · Sala de retos"; Width = 680; Height = 700; MinWidth = 570; MinHeight = 560;
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/bael-unicorn.ico"));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(22) };
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var heading = new DockPanel();
        var avatar = new Image { Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory,"Sprites","desktop","gamer-0.png"))), Height = 92, Width = 92 };
        heading.Children.Add(avatar);
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock { Text = "La mesa de BAEL", FontSize = 25, FontWeight = FontWeights.Bold });
        title.Children.Add(new TextBlock { Text = "Fichas ficticias · sin compras ni dinero real", Foreground = Brushes.SlateGray });
        heading.Children.Add(title); root.Children.Add(heading);
        root.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(239,231,255)), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Margin = new Thickness(0,10,0,10),
            Child = new TextBlock { Text = "MODO ESPECTÁCULO: BAEL siempre gana. Los vasos y las cartas están preparados para su victoria. Juega por la animación y las bromas; no es una partida de azar justa.", TextWrapping = TextWrapping.Wrap } });
        var tabs = new WrapPanel(); tabs.Children.Add(cupsTab); tabs.Children.Add(pokerTab); root.Children.Add(tabs);
        cupsTab.Click += (_, _) => SelectGame(false); pokerTab.Click += (_, _) => SelectGame(true);
        balance.FontSize = 17; balance.FontWeight = FontWeights.SemiBold; balance.Margin = new Thickness(0,10,0,10); root.Children.Add(balance);
        var bets = new WrapPanel(); bets.Children.Add(new TextBlock { Text = "Fichas por ronda: ", VerticalAlignment = VerticalAlignment.Center }); bets.Children.Add(stake); bets.Children.Add(start); root.Children.Add(bets);
        start.Click += async (_, _) => await StartRound();
        status.TextWrapping = TextWrapping.Wrap; status.Margin = new Thickness(0,12,0,10); root.Children.Add(status);
        table.MinHeight = 180; root.Children.Add(table);
        draw.Click += (_, _) => FinishPoker(); root.Children.Add(draw);
        result.TextWrapping = TextWrapping.Wrap; result.FontSize = 15; result.Margin = new Thickness(0,12,0,12); root.Children.Add(result);
        var refill = new Button { Content = "Recargar 1.000 fichas al quedarme en cero", HorizontalAlignment = HorizontalAlignment.Left };
        refill.Click += (_, _) => { if (session.Refill()) { result.Text = "Saldo ficticio recargado. Puedes cerrar la sala cuando quieras."; UpdateControls(); } else result.Text = "La recarga está disponible al terminar una ronda con saldo cero."; };
        root.Children.Add(refill);
        root.Children.Add(new TextBlock { Text = "El saldo dura mientras esta ventana siga abierta. En póker juegas una mano de cinco cartas y puedes cambiar hasta tres. BAEL revela su escalera real al final.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.SlateGray, Margin = new Thickness(0,12,0,0) });
        Closed += (_, _) => lifetime.Cancel();
        SelectGame(false);
    }
    internal void SelectGame(bool isPoker)
    {
        if (busy || session.Active) { status.Text = "Termina la ronda actual antes de cambiar de juego."; return; }
        poker = isPoker; result.Text = ""; table.Children.Clear(); picks.Clear();
        status.Text = poker ? "Póker de exhibición: mira tu mano, cambia hasta tres cartas y descubre la jugada preparada de BAEL." : "Mira cómo BAEL mezcla los vasos y elige uno. La pelota siempre acabará en otro: el truco está anunciado.";
        draw.Visibility = poker ? Visibility.Visible : Visibility.Collapsed;
        UpdateControls();
    }
    private void UpdateControls()
    {
        balance.Text = $"Tu saldo: {session.Balance:N0} fichas · Rondas: {session.Rounds}";
        start.IsEnabled = !busy && !session.Active && session.Balance >= 10;
        stake.IsEnabled = !busy && !session.Active;
        cupsTab.IsEnabled = pokerTab.IsEnabled = !busy && !session.Active;
        draw.IsEnabled = poker && session.Active && !busy;
    }
    private async Task StartRound()
    {
        if (!session.Begin(stake.SelectedItem is int n ? n : 50)) { status.Text = "Elige una apuesta que quepa en tu saldo ficticio."; return; }
        result.Text = ""; table.Children.Clear(); picks.Clear();
        if (poker) { session.DealPoker(); ShowCards(false); UpdateControls(); return; }
        busy = true; UpdateControls();
        status.Text = "Mezclando… Sigue los vasos con la vista. ¡El mago tiene el guion de su lado!";
        var canvas = new Canvas { Width = 450, Height = 160, HorizontalAlignment = HorizontalAlignment.Center };
        table.Children.Add(canvas);
        var cups = new Button[3]; int[] positions = [0,1,2];
        for (int i=0;i<3;i++)
        {
            int chosen = i;
            var cup = new Button { Content = $"▰\nVaso {i+1}", FontSize = 23, Width = 120, Height = 110, IsEnabled = false, RenderTransform = new TranslateTransform() };
            Canvas.SetLeft(cup, i*150); Canvas.SetTop(cup,20); canvas.Children.Add(cup); cups[i] = cup;
            cup.Click += (_, _) =>
            {
                if (busy || !session.Active) return;
                int ball = session.RevealCup(chosen);
                for (int c=0;c<3;c++) { cups[c].IsEnabled = false; cups[c].Content = c==ball ? "●\n¡Aquí!" : "Vacío"; }
                Announce($"Elegiste el vaso {chosen+1}; la pelota está en el {ball+1}. BAEL gana {session.Stake} fichas ficticias. El truco teatral mueve la pelota fuera de tu elección.");
                UpdateControls();
            };
        }
        try
        {
            for (int swap=0;swap<8;swap++)
            {
                int a=Random.Shared.Next(3), b=(a+Random.Shared.Next(1,3))%3;
                (positions[a],positions[b])=(positions[b],positions[a]);
                for (int i=0;i<3;i++)
                    ((TranslateTransform)cups[i].RenderTransform).BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation((positions[i]-i)*150, TimeSpan.FromMilliseconds(260)));
                await Task.Delay(290, lifetime.Token);
            }
            busy=false; foreach(var cup in cups) cup.IsEnabled=true;
            status.Text = "Elige un vaso. Recuerda: esta es la trampa teatral de BAEL.";
            UpdateControls();
        }
        catch (OperationCanceledException) { }
    }
    private void ShowCards(bool reveal)
    {
        table.Children.Clear(); picks.Clear();
        table.Children.Add(new TextBlock { Text = "Tu mano" });
        var hand = new WrapPanel(); table.Children.Add(hand);
        foreach (var card in session.Hand)
        {
            var face = new TextBlock { Text = card.Label, FontSize = 26, Margin = new Thickness(8), Foreground = card.Suit is "♥" or "♦" ? Brushes.Crimson : Brushes.DarkSlateBlue };
            var pick = new CheckBox { Content = face, Margin = new Thickness(5), IsEnabled = !reveal, ToolTip = "Marca para cambiar esta carta (máximo tres)" };
            hand.Children.Add(pick); picks.Add(pick);
        }
        table.Children.Add(new TextBlock { Text = reveal ? "BAEL: " + string.Join("  ",GameSession.House.Select(c=>c.Label)) + " · Escalera real" : "BAEL: ▣  ▣  ▣  ▣  ▣ · Mano preparada de exhibición", FontSize=17, Margin=new Thickness(0,14,0,0), TextWrapping=TextWrapping.Wrap });
        status.Text = reveal ? "Ronda terminada. Puedes empezar otra o volver al escritorio." : "Marca hasta tres cartas y pulsa Cambiar y revelar. Puedes conservarlas todas.";
    }
    private void FinishPoker()
    {
        if (!session.Active || busy) return;
        int[] selected = picks.Select((box,index)=>(box,index)).Where(x=>x.box.IsChecked==true).Select(x=>x.index).ToArray();
        if (selected.Length>3) { status.Text="Puedes cambiar un máximo de tres cartas."; return; }
        string hand = session.FinishPoker(selected); ShowCards(true);
        Announce($"Tu mano: {hand}. BAEL revela una escalera real y gana {session.Stake} fichas ficticias. Era la victoria preparada del modo espectáculo.");
        UpdateControls();
    }
    private void Announce(string text)
    {
        string[] taunts = ["¿Te atreves con otra ronda de fichas imaginarias?", "¡Mi cara de victoria venía incluida con el traje!", "Tienes valor para retar al unicornio. Eso ya merece aplausos."];
        result.Text = text + (personality.Humor>0 ? "\n"+taunts[Random.Shared.Next(taunts.Length)] : "");
        Comment?.Invoke(personality.FormatLocal(ChatMode.Gamer,result.Text));
    }
    internal void VerifyInterface()
    {
        SelectGame(true);
        if (!poker || ModeActivities.Catalog(ChatMode.Gamer).Length<2) throw new InvalidOperationException("La sala gamer no abrió.");
        StartRound().GetAwaiter().GetResult();
        if (picks.Count != 5 || !draw.IsEnabled) throw new InvalidOperationException("No se repartieron las cartas.");
        picks[0].IsChecked = true;
        FinishPoker();
        if (session.Active || !result.Text.Contains("Escalera") && !result.Text.Contains("escalera"))
            throw new InvalidOperationException("El póker no terminó.");
        SelectGame(false);
    }
}
