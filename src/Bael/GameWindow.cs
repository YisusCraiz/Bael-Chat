using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.IO;

namespace Bael;

public sealed class GameWindow : Window
{
    private readonly GameSession session = new();
    private readonly ModePersonality personality;
    private readonly TextBlock balance = new(), status = new(), result = new(), steps = new(), gameTitle = new(), selection = new();
    private readonly Button refill = new() { Content="Recargar 1.000 fichas gratis",HorizontalAlignment=HorizontalAlignment.Left };
    private readonly ComboBox stake = new() { Width = 110, ItemsSource = new[] { 10, 50, 100 }, SelectedIndex = 1 };
    private readonly Button start = new() { Content = "Empezar ronda →" };
    private readonly Button draw = new() { Content = "Cambiar y revelar", IsEnabled = false };
    private readonly StackPanel table = new();
    private readonly Button cupsTab = new() { Content = "Vasos" }, pokerTab = new() { Content = "Póker" };
    private readonly List<CheckBox> picks = new();
    private readonly CancellationTokenSource lifetime = new();
    private bool poker, busy;
    internal event Action<string>? Comment;

    internal GameWindow(ModePersonality personality,string theme="Claro")
    {
        this.personality = personality;
        Title = "BAEL · Sala de retos"; Width = 850; Height = 850; MinWidth = 680; MinHeight = 650;
        SettingsTheme.Apply(this,theme=="Oscuro");
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/bael-unicorn.ico"));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(22) };
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var heading = new DockPanel();
        var portrait=new BitmapImage();portrait.BeginInit();portrait.CacheOption=BitmapCacheOption.OnLoad;
        portrait.UriSource=new Uri(Path.Combine(AppContext.BaseDirectory,"Sprites","desktop","gamer-0.png"));portrait.EndInit();portrait.Freeze();
        var avatar = new Image { Source = DesktopWindow.WheelPortrait(portrait), Height = 100, Width = 100,Margin=new Thickness(0,0,14,0) };
        heading.Children.Add(avatar);
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock { Text = "BAEL · SALA DE RETOS", FontSize = 11, Foreground=Brushes.White,FontWeight = FontWeights.Bold });
        title.Children.Add(new TextBlock { Text = "El truco también es parte del juego.", FontSize = 25,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.White, FontWeight = FontWeights.Bold,Margin=new Thickness(0,5,0,5) });
        title.Children.Add(new TextBlock { Text = "Fichas ficticias · sin compras ni dinero real", Foreground = Brushes.White });
        heading.Children.Add(title);
        root.Children.Add(new Border { Background=new LinearGradientBrush(Color.FromRgb(48,40,93),Color.FromRgb(8,126,111),45),CornerRadius=new CornerRadius(20),Padding=new Thickness(16),Child=heading });
        root.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(239,231,255)), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Margin = new Thickness(0,12,0,12),
            Child = new TextBlock { Foreground=Brushes.DarkSlateBlue,Text = "ESPECTÁCULO ANUNCIADO · BAEL siempre gana. Las cartas y la pelota están preparadas para su victoria. Tú juegas por las animaciones y las bromas; no es una partida de azar justa.", TextWrapping = TextWrapping.Wrap } });
        var tabs = new UniformGrid { Columns=2 }; tabs.Children.Add(cupsTab); tabs.Children.Add(pokerTab); root.Children.Add(tabs);
        cupsTab.Click += (_, _) => SelectGame(false); pokerTab.Click += (_, _) => SelectGame(true);
        gameTitle.FontSize=21;gameTitle.FontWeight=FontWeights.Bold;gameTitle.Margin=new Thickness(0,12,0,5);root.Children.Add(gameTitle);
        steps.TextWrapping=TextWrapping.Wrap;steps.Margin=new Thickness(0,0,0,12);root.Children.Add(steps);
        balance.FontSize = 17; balance.FontWeight = FontWeights.SemiBold; balance.Margin = new Thickness(0,0,0,10);
        var controls=new StackPanel(); controls.Children.Add(balance);
        var bets = new WrapPanel(); bets.Children.Add(new TextBlock { Text = "1 · Fichas por ronda: ", VerticalAlignment = VerticalAlignment.Center }); bets.Children.Add(stake); bets.Children.Add(start); controls.Children.Add(bets);
        start.Click += async (_, _) => await StartRound();
        var controlCard=new Border { CornerRadius=new CornerRadius(14),Padding=new Thickness(14),Child=controls };controlCard.SetResourceReference(BackgroundProperty,"UiPanel");root.Children.Add(controlCard);
        status.TextWrapping = TextWrapping.Wrap; status.Margin = new Thickness(0,12,0,10);status.FontWeight=FontWeights.SemiBold;System.Windows.Automation.AutomationProperties.SetLiveSetting(status,System.Windows.Automation.AutomationLiveSetting.Polite); root.Children.Add(status);
        table.MinHeight = 150;
        var playCard=new Border { CornerRadius=new CornerRadius(16),Padding=new Thickness(14),Child=table };playCard.SetResourceReference(BackgroundProperty,"UiInset");root.Children.Add(playCard);
        selection.Margin=new Thickness(0,10,0,6);selection.TextWrapping=TextWrapping.Wrap;root.Children.Add(selection);
        draw.Click += (_, _) => FinishPoker(); root.Children.Add(draw);
        result.TextWrapping = TextWrapping.Wrap; result.FontSize = 15; result.Margin = new Thickness(0,12,0,12); root.Children.Add(result);
        refill.Click += (_, _) => { if (session.Refill()) { result.Text = "Saldo ficticio recargado. Puedes cerrar la sala cuando quieras."; UpdateControls(); } else result.Text = "La recarga está disponible al terminar una ronda con saldo cero."; };
        root.Children.Add(refill);
        root.Children.Add(new Expander { Header="Reglas y saldo de esta sala",Margin=new Thickness(0,12,0,0),Content=new TextBlock { Text="Cada ventana empieza con 1.000 fichas. Las rondas descuentan 10, 50 o 100. Al llegar a cero puedes recargar gratis. El saldo no se guarda al cerrar. En póker recibes cinco cartas y puedes cambiar hasta tres; no es una mesa completa con turnos de apuestas. BAEL revela su escalera real al final. Termina tu ronda antes de cambiar entre Vasos y Póker. Cambiar de modo cierra esta sala.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0) } });
        Closed += (_, _) => lifetime.Cancel();
        SelectGame(false);
    }
    internal void SelectGame(bool isPoker)
    {
        if (busy || session.Active) { status.Text = "Termina la ronda actual antes de cambiar de juego."; return; }
        poker = isPoker; result.Text = ""; table.Children.Clear(); picks.Clear();
        gameTitle.Text=poker?"Tu mano contra el guion de BAEL":"¿Dónde acabó la pelota?";
        steps.Text=poker?"1 · Elige tus fichas e inicia    2 · Marca hasta tres cartas    3 · Cambia y revela":"1 · Elige tus fichas e inicia    2 · Mira cómo se mezclan    3 · Pulsa un vaso";
        table.Children.Add(new TextBlock { Text=poker?"♠   ♥   ♣   ♦\nTus cinco cartas aparecerán al empezar la ronda.":"▰      ▰      ▰\nLos tres vasos aparecerán al empezar la ronda.",FontSize=23,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,25,8,25) });
        selection.Visibility=poker?Visibility.Visible:Visibility.Collapsed;
        selection.Text=poker?"Puedes conservar las cinco cartas: no es obligatorio cambiarlas.":"";
        cupsTab.SetResourceReference(BackgroundProperty,poker?"UiPanel":"UiAccent");cupsTab.SetResourceReference(ForegroundProperty,poker?"UiText":"UiOnAccent");
        pokerTab.SetResourceReference(BackgroundProperty,poker?"UiAccent":"UiPanel");pokerTab.SetResourceReference(ForegroundProperty,poker?"UiOnAccent":"UiText");
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
        UpdatePokerSelection();
        refill.IsEnabled=!session.Active && !busy && session.Balance==0;
        start.Content=session.Balance<10?"Recarga fichas para continuar":"Empezar ronda →";
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
            if(lifetime.IsCancellationRequested) return;
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
            var face = new Border { Background=Brushes.White,BorderBrush=Brushes.Lavender,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(10),MinWidth=60,Height=86,Child=new TextBlock { Text=card.Label,FontSize=26,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Foreground=card.Suit is "♥" or "♦"?Brushes.Crimson:Brushes.DarkSlateBlue } };
            var pick = new CheckBox { Content = face, Margin = new Thickness(5), IsEnabled = !reveal, ToolTip = "Marca para cambiar esta carta (máximo tres)" };
            pick.Checked+=(_,_)=>UpdatePokerSelection();pick.Unchecked+=(_,_)=>UpdatePokerSelection();
            hand.Children.Add(pick); picks.Add(pick);
        }
        table.Children.Add(new TextBlock { Text = reveal ? "BAEL: " + string.Join("  ",GameSession.House.Select(c=>c.Label)) + " · Escalera real" : "BAEL: ▣  ▣  ▣  ▣  ▣ · Mano preparada de exhibición", FontSize=17, Margin=new Thickness(0,14,0,0), TextWrapping=TextWrapping.Wrap });
        status.Text = reveal ? "Ronda terminada. Puedes empezar otra o volver al escritorio." : "Marca hasta tres cartas y pulsa Cambiar y revelar. Puedes conservarlas todas.";
    }
    private void UpdatePokerSelection()
    {
        int count=picks.Count(p=>p.IsChecked==true);
        draw.IsEnabled=poker && session.Active && !busy && count<=3;
        if(poker && session.Active) selection.Text=$"{count} de 3 cartas marcadas · "+(count>3?"desmarca alguna para continuar.":count==0?"Conservarás tu mano completa.":"Solo se cambiarán las marcadas.");
        else if(poker) selection.Text="Puedes iniciar otra ronda cuando quieras.";
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
        status.Text="Ronda terminada · puedes empezar otra o elegir el otro juego.";
        string[] taunts = ["¿Te atreves con otra ronda de fichas imaginarias?", "¡Mi cara de victoria venía incluida con el traje!", "Tienes valor para retar al unicornio. Eso ya merece aplausos."];
        result.Text = text + (personality.Humor>0 ? "\n"+taunts[Random.Shared.Next(taunts.Length)] : "");
        Comment?.Invoke(personality.FormatLocal(ChatMode.Gamer,result.Text));
    }
    internal async Task VerifyCupsAsync()
    {
        SelectGame(false);int rounds=session.Rounds;int previous=session.Balance;
        var mixing=StartRound();
        if(!busy || !session.Active || cupsTab.IsEnabled || pokerTab.IsEnabled) throw new InvalidOperationException("Los vasos permiten cambiar de juego durante la mezcla.");
        SelectGame(true);if(poker) throw new InvalidOperationException("Se cambió de juego durante una ronda.");
        await mixing;
        var cups=((Canvas)table.Children[0]).Children.OfType<Button>().ToArray();
        if(busy || cups.Length!=3 || cups.Any(c=>!c.IsEnabled)) throw new InvalidOperationException("La mezcla no habilitó los tres vasos.");
        cups[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(session.Active || session.Rounds!=rounds+1 || session.Balance!=previous-session.Stake || cups.Any(c=>c.IsEnabled) || !status.Text.Contains("Ronda terminada")) throw new InvalidOperationException("Los vasos no cerraron una sola ronda.");
        cups[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(session.Rounds!=rounds+1) throw new InvalidOperationException("Una segunda pulsación duplicó el resultado de vasos.");
    }
    internal void VerifyInterface()
    {
        SelectGame(true);
        if (!poker || ModeActivities.Catalog(ChatMode.Gamer).Length<2) throw new InvalidOperationException("La sala gamer no abrió.");
        StartRound().GetAwaiter().GetResult();
        if (picks.Count != 5 || !draw.IsEnabled) throw new InvalidOperationException("No se repartieron las cartas.");
        for(int i=0;i<4;i++) picks[i].IsChecked=true;
        if(draw.IsEnabled) throw new InvalidOperationException("El póker permitió cambiar más de tres cartas.");
        for(int i=1;i<4;i++) picks[i].IsChecked=false;
        if(!draw.IsEnabled) throw new InvalidOperationException("No se pudo corregir la selección de cartas.");
        FinishPoker();
        if (session.Active || !result.Text.Contains("Escalera") && !result.Text.Contains("escalera"))
            throw new InvalidOperationException("El póker no terminó.");
        SelectGame(false);
    }
}
