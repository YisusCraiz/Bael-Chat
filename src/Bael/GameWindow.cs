using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.IO;
using ShapePath=System.Windows.Shapes.Path;
using Ellipse=System.Windows.Shapes.Ellipse;
using System.Windows.Media.Effects;

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
    private int lastTaunt=-1;
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
        table.MinHeight = 170;
        table.SetValue(System.Windows.Documents.TextElement.ForegroundProperty,Brushes.White);
        var playCard=new Border { CornerRadius=new CornerRadius(22),Padding=new Thickness(18),BorderThickness=new Thickness(2),BorderBrush=new SolidColorBrush(Color.FromRgb(168,139,79)),Background=new LinearGradientBrush(Color.FromRgb(22,79,77),Color.FromRgb(20,39,58),45),Child=table };root.Children.Add(playCard);
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
        status.Text = poker ? "Mira tu mano, cambia hasta tres cartas y revela la jugada." : "Mira la mezcla y elige un vaso al terminar.";
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
        if (!session.Begin(stake.SelectedItem is int n ? n : 50)) { status.Text = "Elige una cantidad que quepa en tu saldo."; return; }
        result.Text = ""; table.Children.Clear(); picks.Clear();
        if (poker) { session.DealPoker(); ShowCards(false); UpdateControls(); return; }
        busy = true; UpdateControls();
        status.Text = "Mezclando… ¡No me pierdas de vista!";
        var canvas = new Canvas { Width = 450, Height = 185, HorizontalAlignment = HorizontalAlignment.Center };
        table.Children.Add(canvas);
        var cups = new Button[3]; int[] positions = [0,1,2];
        for (int i=0;i<3;i++)
        {
            int chosen = i;
            var cup = new Button { Content = CupFace(i+1,false,false), Width = 126, Height = 154,Padding=new Thickness(0),Margin=new Thickness(0),Background=Brushes.Transparent,BorderBrush=Brushes.Transparent, IsEnabled = false, RenderTransform = new TranslateTransform(),ToolTip=$"Elegir vaso {i+1}" };
            var template=new ControlTemplate(typeof(Button));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);template.VisualTree=presenter;cup.Template=template;
            cup.MouseEnter+=(_,_)=> { if(cup.IsEnabled) cup.Opacity=0.88; };cup.MouseLeave+=(_,_)=>cup.Opacity=1;
            Canvas.SetLeft(cup, i*150); Canvas.SetTop(cup,20); canvas.Children.Add(cup); cups[i] = cup;
            cup.Click += (_, _) =>
            {
                if (busy || !session.Active) return;
                int ball = session.RevealCup(chosen);
                for (int c=0;c<3;c++) { cups[c].IsEnabled = false; cups[c].Opacity=1; cups[c].Content = CupFace(c+1,c==ball,true); }
                Announce($"Elegiste el vaso {chosen+1}; la pelota está en el {ball+1}. Esta ronda es mía: {session.Stake} fichas.");
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
                        new DoubleAnimation((positions[i]-i)*150, TimeSpan.FromMilliseconds(280)) { EasingFunction=new QuadraticEase { EasingMode=EasingMode.EaseInOut } });
                foreach(int moving in new[]{a,b}) ((TranslateTransform)cups[moving].RenderTransform).BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,-12,TimeSpan.FromMilliseconds(140)) { AutoReverse=true,EasingFunction=new SineEase() });
                await Task.Delay(310, lifetime.Token);
            }
            if(lifetime.IsCancellationRequested) return;
            busy=false; foreach(var cup in cups) cup.IsEnabled=true;
            status.Text = "Listo. ¿Con qué vaso te quedas?";
            UpdateControls();
        }
        catch (OperationCanceledException) { }
    }
    private static FrameworkElement CupFace(int number,bool ball,bool reveal)
    {
        var root=new Grid { Width=118,Height=152 };
        root.Children.Add(new Ellipse { Width=104,Height=17,Fill=new SolidColorBrush(Color.FromArgb(90,0,0,0)),VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,15) });
        if(reveal)
        {
            var bottom=new StackPanel { VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center };
            if(ball) bottom.Children.Add(new Ellipse { Width=26,Height=26,Fill=new RadialGradientBrush(Colors.White,Colors.Gold),Effect=new DropShadowEffect { Color=Colors.Gold,BlurRadius=15,ShadowDepth=0,Opacity=0.6 } });
            bottom.Children.Add(new TextBlock { Text=ball?"¡Aquí!":"Vacío",Foreground=Brushes.White,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,4,0,0) });
            root.Children.Add(bottom);
        }
        var body=new Canvas { Width=118,Height=122,VerticalAlignment=VerticalAlignment.Top,RenderTransform=new TranslateTransform() };
        body.Children.Add(new ShapePath { Data=Geometry.Parse("M 29,16 Q 59,5 89,16 L 108,104 Q 59,121 10,104 Z"),Fill=new LinearGradientBrush(Color.FromRgb(160,112,244),Color.FromRgb(73,32,146),0),Stroke=new SolidColorBrush(Color.FromRgb(221,193,255)),StrokeThickness=2,Effect=new DropShadowEffect { BlurRadius=12,ShadowDepth=5,Opacity=0.35 } });
        var shine=new ShapePath { Data=Geometry.Parse("M 34,25 L 27,98"),Stroke=new SolidColorBrush(Color.FromArgb(100,255,255,255)),StrokeThickness=5 };body.Children.Add(shine);
        var top=new Ellipse { Width=60,Height=15,Fill=new SolidColorBrush(Color.FromRgb(82,40,146)),Stroke=Brushes.Lavender,StrokeThickness=2 };Canvas.SetLeft(top,29);Canvas.SetTop(top,9);body.Children.Add(top);
        var star=new TextBlock { Text="★",FontSize=33,Foreground=Brushes.LemonChiffon };Canvas.SetLeft(star,43);Canvas.SetTop(star,39);body.Children.Add(star);
        var badge=new Border { Width=29,Height=29,Background=Brushes.White,CornerRadius=new CornerRadius(15),Child=new TextBlock { Text=number.ToString(),Foreground=Brushes.Indigo,FontWeight=FontWeights.Bold,FontSize=17,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center } };Canvas.SetLeft(badge,44);Canvas.SetTop(badge,85);body.Children.Add(badge);
        root.Children.Add(body);
        if(reveal) ((TranslateTransform)body.RenderTransform).BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,-32,TimeSpan.FromMilliseconds(420)) { EasingFunction=new BackEase { Amplitude=0.3,EasingMode=EasingMode.EaseOut } });
        return root;
    }
    private static Border CardFace(PlayingCard? card,bool reveal,int order)
    {
        bool red=card?.Suit is "♥" or "♦";Brush ink=red?Brushes.Crimson:Brushes.MidnightBlue;
        var details=new Grid();
        if(card is null)
        {
            details.Children.Add(new TextBlock { Text="✦",FontSize=46,Foreground=Brushes.LemonChiffon,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center });
        }
        else
        {
            details.Children.Add(new TextBlock { Text=card.Label,FontSize=17,Foreground=ink,FontWeight=FontWeights.Bold,Margin=new Thickness(5),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top });
            details.Children.Add(new TextBlock { Text=card.Suit,FontSize=42,Foreground=ink,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center });
            details.Children.Add(new TextBlock { Text=card.Label,FontSize=17,Foreground=ink,FontWeight=FontWeights.Bold,Margin=new Thickness(5),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom });
        }
        var transforms=new TransformGroup();var flip=new ScaleTransform();var slide=new TranslateTransform();transforms.Children.Add(flip);transforms.Children.Add(slide);
        var face=new Border { Width=82,Height=120,CornerRadius=new CornerRadius(11),Background=card is null?new LinearGradientBrush(Colors.Indigo,Colors.MediumPurple,45):new SolidColorBrush(Color.FromRgb(255,252,246)),BorderBrush=Brushes.Lavender,BorderThickness=new Thickness(1),Child=details,RenderTransformOrigin=new Point(.5,.5),RenderTransform=transforms,Effect=new DropShadowEffect { BlurRadius=9,ShadowDepth=4,Opacity=0.3 } };
        var delay=TimeSpan.FromMilliseconds(order*75);
        slide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(35,0,TimeSpan.FromMilliseconds(350)) { BeginTime=delay,EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } });
        face.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(280)) { BeginTime=delay });
        if(reveal) flip.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(0.06,1,TimeSpan.FromMilliseconds(420)) { BeginTime=delay,EasingFunction=new SineEase { EasingMode=EasingMode.EaseOut } });
        return face;
    }
    private static void HighlightCard(Border face,bool chosen)
    {
        face.BorderBrush=chosen?Brushes.Gold:Brushes.Lavender;face.BorderThickness=new Thickness(chosen?3:1);
        if(face.RenderTransform is TransformGroup group && group.Children[1] is TranslateTransform slide) slide.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(chosen?-9:0,TimeSpan.FromMilliseconds(130)));
    }
    private void ShowCards(bool reveal)
    {
        table.Children.Clear(); picks.Clear();
        table.Children.Add(new TextBlock { Text = "Tu mano" });
        var hand = new WrapPanel(); table.Children.Add(hand);
        foreach (var card in session.Hand)
        {
            var face = CardFace(card,reveal,picks.Count);
            var pick = new CheckBox { Content = face, Margin = new Thickness(4,10,4,10), IsEnabled = !reveal, ToolTip = "Marca para cambiar esta carta (máximo tres)" };
            pick.Checked+=(_,_)=> { HighlightCard(face,true);UpdatePokerSelection(); };pick.Unchecked+=(_,_)=> { HighlightCard(face,false);UpdatePokerSelection(); };
            hand.Children.Add(pick); picks.Add(pick);
        }
        table.Children.Add(new TextBlock { Text=reveal?"BAEL · Escalera real":"BAEL · cartas reservadas",FontSize=16,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,18,0,5) });
        var house=new WrapPanel();int order=0;
        foreach(var card in GameSession.House) { var face=CardFace(reveal?card:null,reveal,order++);face.Margin=new Thickness(4,8,4,8);house.Children.Add(face); }
        table.Children.Add(house);
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
        Announce($"Tu mano: {hand}. Mi escalera real se lleva {session.Stake} fichas.");
        UpdateControls();
    }
    private void Announce(string text)
    {
        status.Text="Ronda terminada · puedes empezar otra o elegir el otro juego.";
        string[] taunts = personality.Humor>=2
            ? ["Mi pose de campeón ya tenía ensayo.","Acepto aplausos; con uno dramático me basta.","El cuerno es de adorno. El estilo viene de fábrica.","Me salió redonda. Qué conveniente para mi ego."]
            : ["Buen intento.","Esta me salió bien.","Me quedo con esta jugada.","Anotada la ronda."];
        int choice;do { choice=Random.Shared.Next(taunts.Length); } while(choice==lastTaunt);lastTaunt=choice;
        result.Text = text + (personality.Humor>0 ? "\n"+taunts[choice] : "");
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
