using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace Bael;

internal sealed record MemoryMode(ChatMode Mode,string Name);
internal sealed class MemoryWindow : Window
{
    private readonly MemoryData data;
    private readonly Action changed;
    private readonly Action<ChatMode> clearHistory;
    private readonly Func<bool> reset, canEdit;
    private readonly Func<string> statusText;
    private readonly BaelSettings settings;
    private readonly ComboBox modes = new() { DisplayMemberPath="Name" };
    private readonly TextBox search = new(), noteText = new() { AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=76,MaxLength=500,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
    private readonly ListBox messages = new() { Height=190 }, notes = new() { Height=110,DisplayMemberPath="Label" };
    private readonly CheckBox enabled = new() { Content="Usar este recuerdo al conversar con IA",IsChecked=true };
    private readonly TextBlock notice = new() { TextWrapping=TextWrapping.Wrap }, status = new() { TextWrapping=TextWrapping.Wrap }, rule = new() { TextWrapping=TextWrapping.Wrap };
    private readonly DispatcherTimer timer = new() { Interval=TimeSpan.FromMilliseconds(500) };
    private string? editing;
    private ChatMode Mode => (modes.SelectedItem as MemoryMode)?.Mode ?? ChatMode.Free;
    internal MemoryWindow(MemoryData data,BaelSettings settings,ChatMode initial,Action changed,
        Action<ChatMode> clearHistory,Func<bool> reset,Func<bool> canEdit,Func<string> statusText)
    {
        this.data=data; this.settings=settings; this.changed=changed; this.clearHistory=clearHistory;
        this.reset=reset; this.canEdit=canEdit; this.statusText=statusText;
        Title="BAEL · Historial y recuerdos"; Width=760; Height=760; MinWidth=560; MinHeight=480;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        SettingsTheme.Apply(this,settings.UiTheme=="Oscuro");
        SetResourceReference(BackgroundProperty,"UiSurface"); SetResourceReference(ForegroundProperty,"UiText");
        SourceInitialized+=(_,_)=>SettingsTheme.TitleBar(this,settings.UiTheme=="Oscuro");
        var root=new DockPanel { Margin=new Thickness(20) }; Content=root;
        var close=new Button { Content="Cerrar",HorizontalAlignment=HorizontalAlignment.Right,IsCancel=true };
        close.Click+=(_,_)=>Close(); DockPanel.SetDock(close,Dock.Bottom); root.Children.Add(close);
        var bottom=new StackPanel { Margin=new Thickness(0,10,0,8) }; bottom.Children.Add(status);
        DockPanel.SetDock(bottom,Dock.Bottom); root.Children.Add(bottom);
        var body=new StackPanel(); root.Children.Add(new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
        body.Children.Add(new TextBlock { Text="Retoma la conversación",FontSize=23,FontWeight=FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text="Historial separado por modo y recuerdos que eliges guardar. Los cambios aquí se aplican al instante.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,7,0,12) });
        body.Children.Add(new Label { Content="Modo" }); body.Children.Add(modes);
        modes.ItemsSource=Enum.GetValues<ChatMode>().Select(m=>new MemoryMode(m,Responses.Name(m))).ToArray();
        modes.SelectionChanged+=(_,_)=> { editing=null; noteText.Clear(); enabled.IsChecked=true; notice.Text=""; Refresh(); };
        body.Children.Add(rule);
        body.Children.Add(new Label { Content="Buscar en el historial de este modo" }); body.Children.Add(search);
        search.TextChanged+=(_,_)=>RefreshMessages();
        var template=new DataTemplate(); var text=new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty,new Binding("Label")); text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);
        template.VisualTree=text; messages.ItemTemplate=template;
        messages.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled);
        messages.SetValue(ItemsControl.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch);
        body.Children.Add(messages);
        var historyButtons=new WrapPanel();
        historyButtons.Children.Add(ActionButton("Borrar historial de este modo",()=>
        {
            if(!canEdit()) { notice.Text="Restablece el archivo para poder modificar la memoria guardada."; return; }
            if(Confirm("¿Borrar los mensajes y el contexto de "+Responses.Name(Mode)+"? Los recuerdos elegidos se conservan."))
            { clearHistory(Mode); notice.Text="Historial del modo borrado."; RefreshMessages(); }
        })); body.Children.Add(historyButtons);
        body.Children.Add(new TextBlock { Text="Recuerdos elegidos",FontSize=19,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,16,0,6) });
        body.Children.Add(new TextBlock { Text="Por ejemplo: me llamo Yisus, estudio TICs o prefiero ejemplos de anime. Cada modo admite 20 notas de hasta 500 caracteres, con un máximo de 6 activas para la IA. Puedes apagarlas sin borrarlas.",TextWrapping=TextWrapping.Wrap });
        body.Children.Add(notes); body.Children.Add(noteText); body.Children.Add(enabled);
        notes.SelectionChanged+=(_,_)=>
        {
            if(notes.SelectedItem is RememberedNote note)
            { editing=note.Id; noteText.Text=note.Text; enabled.IsChecked=note.Enabled; }
        };
        var noteButtons=new WrapPanel();
        noteButtons.Children.Add(ActionButton("Guardar recuerdo",SaveNote));
        noteButtons.Children.Add(ActionButton("Nueva nota",()=> { editing=null; notes.SelectedItem=null; noteText.Clear(); enabled.IsChecked=true; }));
        noteButtons.Children.Add(ActionButton("Borrar recuerdo",()=>
        {
            if(!canEdit()) { notice.Text="El archivo de memoria necesita restablecerse."; return; }
            if(editing is string id && Confirm("¿Borrar este recuerdo elegido?"))
            { data.Notes.RemoveAll(n=>n.Id==id); editing=null; noteText.Clear(); changed(); Refresh(); notice.Text="Recuerdo borrado."; }
        })); body.Children.Add(noteButtons); body.Children.Add(notice);
        notice.SetResourceReference(TextBlock.ForegroundProperty,"UiError");
        body.Children.Add(ActionButton("Borrar toda la memoria y restablecer el archivo",()=>
        {
            if(Confirm("¿Borrar todos los historiales y recuerdos de BAEL? Esta acción elimina el archivo local de memoria."))
            { if(reset()) { editing=null; noteText.Clear(); Refresh(); notice.Text="Memoria restablecida."; } else notice.Text=statusText(); }
        }));
        body.Children.Add(new TextBlock { Text="Las opciones para guardar historial y compartir contexto o recuerdos con tus APIs están en Ajustes → Memoria. El historial conserva los últimos 100 mensajes por modo y hasta 4.000 caracteres por mensaje. El gestor no exporta ni envía conversaciones por sí solo.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12) });
        modes.SelectedItem=((IEnumerable<MemoryMode>)modes.ItemsSource).First(m=>m.Mode==initial);
        timer.Tick+=(_,_)=>status.Text=statusText(); timer.Start(); status.Text=statusText();
        Closed+=(_,_)=>timer.Stop();
        Loaded+=(_,_)=> { Height=Math.Min(Height,SystemParameters.WorkArea.Height); Width=Math.Min(Width,SystemParameters.WorkArea.Width); };
    }
    internal void VerifyInterface()
    {
        modes.SelectedItem=((IEnumerable<MemoryMode>)modes.ItemsSource).First(m=>m.Mode==ChatMode.Free);
        noteText.Text="Recuerdo de interfaz"; SaveNote();
        if(!data.Notes.Any(n=>n.Mode==ChatMode.Free && n.Text=="Recuerdo de interfaz")) throw new InvalidOperationException("El gestor no guardó una nota.");
        notes.SelectedIndex=0; enabled.IsChecked=false; SaveNote();
        if(data.ContextFor(ChatMode.Free)!="[]") throw new InvalidOperationException("Una nota desactivada siguió activa.");
        modes.SelectedItem=((IEnumerable<MemoryMode>)modes.ItemsSource).First(m=>m.Mode==ChatMode.English);
        if(notes.Items.Count!=0) throw new InvalidOperationException("El gestor mezcló recuerdos entre modos.");
    }

    private Button ActionButton(string label,Action action)
    { var button=new Button { Content=label,HorizontalAlignment=HorizontalAlignment.Left }; button.Click+=(_,_)=>action(); return button; }
    private bool Confirm(string question) => MessageBox.Show(this,question,"Memoria de BAEL",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
    private void RefreshMessages()
    {
        var list=data.Messages.TryGetValue(Mode,out var saved)?saved:[];
        messages.ItemsSource=list.Where(m=>m.Text.Contains(search.Text,StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    private void Refresh()
    {
        RefreshMessages(); notes.ItemsSource=data.Notes.Where(n=>n.Mode==Mode).ToArray();
        rule.Text=settings.GetDocumentRule(Mode).Limited ? "Este modo está limitado a PDF: los recuerdos no se envían al modelo para responder." : "Los recuerdos activos solo se comparten con las APIs si lo habilitas en Ajustes.";
        rule.Margin=new Thickness(0,8,0,8);
    }
    private void SaveNote()
    {
        if(!canEdit()) { notice.Text="El archivo anterior no se puede leer. Restablécelo antes de guardar recuerdos."; return; }
        string value=noteText.Text.Trim();
        if(value.Length==0) { notice.Text="Escribe lo que quieres que BAEL recuerde."; return; }
        if(editing is null && data.Notes.Count(n=>n.Mode==Mode)>=20) { notice.Text="Este modo ya tiene 20 notas."; return; }
        if(enabled.IsChecked==true && data.Notes.Count(n=>n.Mode==Mode && n.Enabled && n.Id!=editing)>=6)
        { notice.Text="Desactiva otra nota para mantener un máximo de seis recuerdos activos."; return; }
        if(data.Notes.Any(n=>n.Mode==Mode && n.Id!=editing && n.Text.Equals(value,StringComparison.OrdinalIgnoreCase)))
        { notice.Text="Ese recuerdo ya existe en este modo."; return; }
        string id=editing ?? Guid.NewGuid().ToString("N");
        data.Notes.RemoveAll(n=>n.Id==id); data.Notes.Add(new(id,Mode,value,enabled.IsChecked==true));
        changed(); editing=null; noteText.Clear(); enabled.IsChecked=true; Refresh(); notice.Text="Recuerdo actualizado. El estado del guardado aparece abajo.";
    }
}
