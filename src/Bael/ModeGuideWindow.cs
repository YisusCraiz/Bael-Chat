using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Bael;

internal sealed record ModeOffer(string Title,string Benefit,string Steps,string Example,string Action,bool Local,bool Draft=false,bool Fallback=false);
internal sealed record ModeGuide(string Title,string Welcome,string Purpose,string Accent,ModeOffer[] Offers)
{
    internal static bool Supports(ChatMode mode)=>mode is ChatMode.Math or ChatMode.Free or ChatMode.Comedy or ChatMode.Tarot or ChatMode.Gamer;
    internal static ModeGuide For(ChatMode mode)=>mode switch
    {
        ChatMode.Math=>new("Tu siguiente paso, no la respuesta.","Yo doy las pistas. Tú construyes la solución.","Practica cómo razonar: intenta un paso, recibe una pista y comprueba tu procedimiento. Los retos locales no necesitan API.","#1767BE",
        [new("Despeja con pistas","Entrena las operaciones inversas en tres pasos.","1 · Lee la ecuación.\n2 · Escribe el número de la operación que aplicarías.\n3 · Avanza cuando el paso sea correcto.","Para 2x + 4 = 10, piensa qué quitarías de ambos lados.","Reto guiado",true),
         new("Entiende las fracciones","Distingue el tamaño de los trozos y cuántos tomas.","1 · Compara los denominadores.\n2 · Decide qué números se combinan.\n3 · Explica la regla sin calcular el resultado final.","Trabajaremos con 1/4 y 2/4; tú eliges cada paso.","Fracciones guiadas",true),
         new("Álgebra paso a paso","Practica sin saltarte la igualdad entre ambos lados.","1 · Identifica la operación.\n2 · Aplica su inversa en ambos lados.\n3 · Comprueba tu razonamiento en el cuaderno.","Practica con 5z − 10 = 15 antes de pasar a tu propio ejercicio.","Álgebra guiada",true),
         new("Revisa tu intento","Comparte tu ejercicio y el paso que te genera dudas.","1 · Escribe el enunciado.\n2 · Añade tu procedimiento.\n3 · Envía tu mensaje para pedir una pista específica.","Ejercicio: 3x + 6 = 18\nMi primer paso: resté 6 en ambos lados. ¿Tiene sentido?","Ejercicio: \nMi primer paso: ",false,true)]),
        ChatMode.Free=>new("Hablemos a nuestro ritmo.","Tu día, tus gustos y alguna aventura inesperada.","Un espacio para conversar sin un tema académico fijo. La charla abierta usa tu API; la aventura y el taller de ideas también funcionan sin conexión.","#8050BA",
        [new("Cuéntame tu día","Empieza por algo que te haya pasado, sin preparar un discurso.","1 · Cuenta un momento de tu día.\n2 · Dime si prefieres que te escuche o que pensemos ideas.\n3 · Envía cuando estés listo.","Hoy me pasó algo en la universidad y quiero contártelo.","Hoy me pasó: ",false,true),
         new("Aventura espacial","Elige el rumbo de una historia corta con BAEL.","1 · Elige cráter o señal.\n2 · Decide si tocas o cantas.\n3 · Descubre tu final. Puedes volver a empezar.","Dos decisiones, una luna violeta y un unicornio con casco.","Aventura espacial",true),
         new("Anime y gustos","Conversa sobre lo que te gusta y descubre puntos en común.","1 · Escribe una serie, juego o afición.\n2 · Cuenta qué te gusta de ella.\n3 · Haz una pregunta para seguir conversando.","Mi anime favorito es… Me gusta porque… ¿Qué opinas?","Quiero hablar de: ",false,true),
         new("Taller de ideas","Convierte una idea inicial en un primer paso concreto.","1 · Cuenta qué quieres crear.\n2 · Elige qué lo hará especial.\n3 · Define una acción pequeña para comenzar.","Una dinámica local para ordenar tus propias ideas.","Taller de ideas",true)]),
        ChatMode.Comedy=>new("Una pausa para reírnos.","Yo pongo el escenario. Tú puedes empeorarlo, fufufu~.","Chistes del campus, escenas improvisadas y una historia por turnos. Los chistes usan tu API cuando está activa y tienen repertorio local de respaldo. Las escenas y las historias por turnos son locales.","#9A5A08",
        [new("Un chiste del campus","Una broma breve para despejarte.","1 · Pulsa empezar.\n2 · Lee o escucha el chiste con los controles del chat.\n3 · Pide otro cuando quieras.","Con IA activa se pide un chiste nuevo usando el tema de la conversación. Sin conexión se usa el repertorio local.","Chiste aleatorio",false,Fallback:true),
         new("Otro chiste","Sigue el recreo con otra ocurrencia del repertorio.","1 · Recibe otra broma.\n2 · Prueba el resto de actividades para participar tú también.","Con IA activa se evita repetir los chistes del historial; sin conexión se usa otro del repertorio local.","Chiste aleatorio",false,Fallback:true),
         new("Escena improvisada","Responde como el profesor en una escena de tres turnos.","1 · BAEL plantea el problema.\n2 · Escribe tu respuesta como profesor.\n3 · Continúa hasta cerrar la escena.","Un unicornio entra en clase alegando que es alumno de intercambio.","Improvisación local",true),
         new("Historia absurda","Escribe una frase por turno y construye un final con BAEL.","1 · Lee el inicio.\n2 · Añade una frase.\n3 · Repite hasta el desenlace. No se evalúa tu respuesta.","Una impresora decide presentarse a las elecciones del campus.","Historia absurda local",true)]),
        ChatMode.Tarot=>new("Una carta, una buena pregunta.","El misterio es teatral. Tus decisiones siguen siendo tuyas.","Cartas imaginarias para explorar ideas, decisiones y metas. Es entretenimiento y reflexión, sin predicciones ni lecturas sobre otras personas. Todas las dinámicas de esta pantalla son locales.","#8050BA",
        [new("Tres cartas","Explora tres preguntas y elige cuál te ayuda a pensar.","1 · Recibe tres cartas imaginarias.\n2 · Elige una de sus preguntas.\n3 · Escribe tu reflexión; la charla abierta requiere API. Para preguntas locales por turnos usa Decisiones o Metas.","Pausa, recursos y siguiente paso: tú decides qué significado darles.","Tirada de 3 cartas",true),
         new("Carta del día","Una pregunta breve para comenzar una reflexión.","1 · Recibe una carta al azar.\n2 · Piensa qué relación tiene contigo.\n3 · Anota una acción que dependa de ti.","No es una predicción ni queda fijada por la fecha; puedes sacar otra. Conversar libremente sobre ella usa la API.","Carta del día",true),
         new("Piensa una decisión","Separa lo que deseas, lo que sabes y lo que puedes hacer.","1 · Describe la decisión sin datos privados de terceros.\n2 · Identifica qué información te falta.\n3 · Elige un paso pequeño y reversible.","Un recorrido guiado de tres preguntas; no decide por ti.","Reflexión de decisiones",true),
         new("Dale forma a una meta","Aterriza una intención en algo que puedas empezar.","1 · Expresa tu meta.\n2 · Reconoce un recurso que ya tienes.\n3 · Propón una acción para esta semana.","Las cartas sirven como invitación para pensar, no como consejo profesional.","Reflexión de metas",true)]),
        ChatMode.Gamer=>new("Entra a la sala de BAEL.","El unicornio tiene el guion de su lado. ¿Aceptas el espectáculo?","Vasos y póker de exhibición: BAEL siempre gana y lo anuncia antes de jugar. Solo hay fichas imaginarias, sin compras ni dinero real. Los minijuegos no necesitan API.","#087E6F",
        [new("Sigue los vasos","Mira la mezcla, elige un vaso y descubre el truco.","1 · Elige 10, 50 o 100 fichas.\n2 · Espera a que termine la animación.\n3 · Pulsa un vaso para revelar la pelota.","La pelota acabará en otro vaso: es un espectáculo anunciado.","@cups",true),
         new("Póker de exhibición","Recibe cinco cartas y elige cuáles cambiar.","1 · Inicia una ronda.\n2 · Marca hasta tres cartas o conserva todas.\n3 · Pulsa Cambiar y revelar. BAEL mostrará su escalera real.","Es una mano de cinco cartas, no una mesa completa con rivales.","@poker",true),
         new("Conoce las reglas","Entiende el saldo y el funcionamiento antes de entrar.","1 · Cada ventana empieza con 1.000 fichas.\n2 · Cada ronda descuenta la cantidad elegida.\n3 · Al llegar a cero puedes recargar gratis.","El saldo solo dura mientras la sala permanezca abierta.","Reglas de los minijuegos",true),
         new("Charla gamer","Habla de juegos, desafíos o estrategias con BAEL.","1 · Cuenta a qué juegas.\n2 · Indica qué reto quieres practicar.\n3 · Envía tu mensaje para conversar.","Estoy jugando a… Quiero mejorar en…","Estoy jugando a: \nQuiero mejorar en: ",false,true)]),
        _=>throw new ArgumentOutOfRangeException(nameof(mode))
    };
}

internal sealed class ModeGuideWindow : Window
{
    private readonly ModeGuide guide;
    private readonly TextBlock detailTitle=new(), detail=new(), example=new(), availability=new();
    private readonly Button begin=new();
    private readonly List<Button> cards=[];
    private int selected;
    internal event Action<ModeOffer>? Start;
    internal ModeGuideWindow(ChatMode mode,BaelSettings settings)
    {
        guide=ModeGuide.For(mode); Title=$"BAEL · {Responses.Name(mode)}";
        Width=1000; Height=800; MinWidth=820; MinHeight=640;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Icon=BitmapFrame.Create(new Uri("pack://application:,,,/Assets/bael-unicorn.ico"));
        SettingsTheme.Apply(this,settings.UiTheme=="Oscuro");
        var root=new Grid { Margin=new Thickness(22) };
        root.RowDefinitions.Add(new() { Height=GridLength.Auto }); root.RowDefinitions.Add(new());
        Content=root;
        var hero=new Grid(); hero.ColumnDefinitions.Add(new(){Width=new GridLength(100)}); hero.ColumnDefinitions.Add(new());
        var portrait=new BitmapImage(); portrait.BeginInit(); portrait.CacheOption=BitmapCacheOption.OnLoad;
        portrait.UriSource=new Uri(Path.Combine(AppContext.BaseDirectory,"Sprites","desktop",Responses.Sprite(mode)+"-0.png")); portrait.EndInit(); portrait.Freeze();
        hero.Children.Add(new Image { Source=DesktopWindow.WheelPortrait(portrait),Height=108,Width=92 });
        var heading=new StackPanel { VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14,0,0,0) }; Grid.SetColumn(heading,1); hero.Children.Add(heading);
        heading.Children.Add(Label("BAEL · "+Responses.Name(mode).ToUpperInvariant(),11,true,Brushes.White));
        heading.Children.Add(Label(guide.Title,26,true,Brushes.White)); heading.Children.Add(Label(guide.Welcome,14,false,Brushes.White));
        root.Children.Add(new Border { CornerRadius=new CornerRadius(20),Padding=new Thickness(16),Margin=new Thickness(0,0,0,14),Background=new LinearGradientBrush(Color.FromRgb(48,40,93),(Color)ColorConverter.ConvertFromString(guide.Accent),45),Child=hero });
        var body=new StackPanel(); var scroll=new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; Grid.SetRow(scroll,1);root.Children.Add(scroll);
        body.Children.Add(Label(guide.Purpose,15));
        body.Children.Add(Label("1 · Elige una actividad    2 · Mira cómo funciona    3 · Empieza",13,true));
        var options=new UniformGrid { Columns=2, Margin=new Thickness(0,8,0,8) }; body.Children.Add(options);
        for(int i=0;i<guide.Offers.Length;i++)
        {
            int index=i; var offer=guide.Offers[i];var content=new StackPanel();
            content.Children.Add(Label(offer.Title,18,true));content.Children.Add(Label(offer.Benefit,13));
            content.Children.Add(Label(offer.Fallback?"IA · respaldo local":offer.Local?"LOCAL · sin API":"CONVERSACIÓN · con API",11,true));
            var button=new Button { Content=content,Padding=new Thickness(14),Margin=new Thickness(0,0,10,10),HorizontalContentAlignment=HorizontalAlignment.Stretch,Tag=i };
            button.SetResourceReference(BackgroundProperty,"UiPanel");button.SetResourceReference(ForegroundProperty,"UiText");button.SetResourceReference(BorderBrushProperty,"UiBorder");
            button.Click+=(_,_)=>Select(index,settings); cards.Add(button);options.Children.Add(button);
        }
        var instructions=new StackPanel(); instructions.Children.Add(detailTitle); instructions.Children.Add(detail);instructions.Children.Add(example);instructions.Children.Add(availability);instructions.Children.Add(begin);
        detailTitle.FontSize=21;detailTitle.FontWeight=FontWeights.Bold;detail.TextWrapping=example.TextWrapping=availability.TextWrapping=TextWrapping.Wrap;
        detail.Margin=new Thickness(0,10,0,10);example.FontStyle=FontStyles.Italic;availability.Margin=new Thickness(0,10,0,10);availability.FontSize=12;
        begin.HorizontalAlignment=HorizontalAlignment.Left;begin.Padding=new Thickness(18,12,18,12);
        begin.Click+=(_,_)=>Start?.Invoke(guide.Offers[selected]);
        var panel=new Border { CornerRadius=new CornerRadius(16),Padding=new Thickness(18),Child=instructions };panel.SetResourceReference(BackgroundProperty,"UiPanel");body.Children.Add(panel);
        var help=new Expander { Header="Controles, voz y cómo salir",Margin=new Thickness(0,14,0,0),Content=Label("Las actividades se abren al pulsar Empezar. Las de conversación preparan un borrador: complétalo y pulsa Enviar. En el chat puedes escribir o usar Hablar; Leer en voz alta usa la salida configurada. Escribe salir actividad para cerrar una dinámica guiada. Cambiar de modo también la cierra. Ajustes permite personalizar el carácter y los servicios. El uso de PDF se configura por modo y tiene prioridad sobre estas actividades.",13) }; body.Children.Add(help);
        Select(0,settings);
    }
    private static TextBlock Label(string text,double size=14,bool bold=false,Brush? foreground=null)
    {
        var label=new TextBlock { Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,FontWeight=bold?FontWeights.Bold:FontWeights.Normal,Margin=new Thickness(0,3,0,4) };
        if(foreground is not null) label.Foreground=foreground;return label;
    }
    private void Select(int index,BaelSettings settings)
    {
        selected=index;var offer=guide.Offers[index];
        detailTitle.Text=offer.Title;detail.Text=offer.Steps;example.Text="Por ejemplo: "+offer.Example;
        availability.Text=offer.Fallback?(settings.OnlineChat?"IA activa: se pedirá un chiste nuevo. Si el proveedor falla se indicará y se usará uno local.":"IA desactivada: se usará el repertorio local. Activa Chat y APIs para generar chistes nuevos."):offer.Local?"Disponible sin conexión. Los resultados se muestran en el chat o la sala de juegos.":settings.OnlineChat?"Conversación con tu API configurada. Se enviará el texto al proveedor cuando pulses Enviar; si falla habrá una guía local básica.":"Conversación con API desactivada. Puedes preparar tu texto; para recibir comentarios abiertos activa tu proveedor en Ajustes. Sin API hay una guía básica, sin revisión personalizada.";
        begin.Content=offer.Draft?"Preparar mi mensaje →":offer.Action.StartsWith('@')?"Abrir el juego →":"Empezar actividad →";
        foreach(var card in cards) { bool active=(int)card.Tag==index;card.SetResourceReference(BackgroundProperty,active?"UiSelected":"UiPanel");card.BorderThickness=new Thickness(active?2:1); }
    }
    internal void VerifyInterface(BaelSettings settings)
    {
        if(cards.Count!=4) throw new InvalidOperationException("Faltan actividades en la guía del modo.");
        ModeOffer? fired=null;Start+=offer=>fired=offer;
        for(int i=0;i<4;i++)
        {
            Select(i,settings);begin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(fired!=guide.Offers[i] || string.IsNullOrWhiteSpace(detail.Text) || !example.Text.Contains(guide.Offers[i].Example)) throw new InvalidOperationException("Una tarjeta no abrió su actividad o instrucciones.");
        }
    }
}
