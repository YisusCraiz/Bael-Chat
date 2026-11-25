using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Bael;

internal sealed record VolumeView(string Name,long Total,long Free)
{
    public double UsedPercent=>Total>0?Math.Clamp(100.0*(Total-Free)/Total,0,100):0;
    public string Detail=>$"{SystemMetrics.Size(Free)} libres de {SystemMetrics.Size(Total)} · {UsedPercent:0}% ocupado";
}
public partial class SystemWindow : Window
{
    private readonly SystemMetrics metrics=new();
    private readonly DispatcherTimer clock=new() { Interval=TimeSpan.FromSeconds(1) };
    private readonly Queue<double> cpuSamples=new();
    private CancellationTokenSource? operation;
    private bool closed;
    private DiskFile[] cleaning=[];
    private readonly string temporaryRoot=Path.GetTempPath();
    private SystemInventory? inventory;
    internal SystemWindow(BaelSettings settings)
    {
        InitializeComponent();
        SettingsTheme.Apply(this,settings.UiTheme=="Oscuro");
        Loaded+=(_,_)=> { RefreshVolumes(); Sample(); clock.Start(); };
        clock.Tick+=(_,_)=>Sample();
        Closed+=(_,_)=> { closed=true; clock.Stop(); operation?.Cancel(); };
        SearchRoot.ItemsSource=DriveInfo.GetDrives().Where(d=>d.DriveType is DriveType.Fixed or DriveType.Removable).Select(d=>d.Name).ToArray();
        SearchRoot.Text=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
    internal void SelectSection(int section) => Tabs.SelectedIndex=Math.Clamp(section,0,Tabs.Items.Count-1);
    private void Sample()
    {
        var value=metrics.Read();
        CpuLabel.Text=value.Cpu is double cpu?$"CPU · {cpu:0}%":"CPU · preparando muestra";
        CpuBar.Value=value.Cpu??0; RamBar.Value=value.Ram??0;
        RamLabel.Text=value.Ram is double ram?$"RAM · {ram:0}% en uso":"RAM · no disponible";
        RamUsage.Text=value.TotalRam>0?$"{SystemMetrics.Size(value.TotalRam-value.FreeRam)} usados de {SystemMetrics.Size(value.TotalRam)} · {SystemMetrics.Size(value.FreeRam)} disponibles":"Windows no expuso la memoria disponible.";
        if(value.Cpu is double sample)
        {
            cpuSamples.Enqueue(sample); while(cpuSamples.Count>60) cpuSamples.Dequeue();
            double width=Math.Max(1,HistoryCanvas.ActualWidth); int index=0;
            CpuHistory.Points=new PointCollection(cpuSamples.Select(v=>new Point(width*index++/59.0,39*(1-v/100))));
        }
    }
    private void RefreshVolumes()
    {
        var rows=new List<VolumeView>(); int skipped=0;
        foreach(var drive in DriveInfo.GetDrives())
            try { if(drive.DriveType is DriveType.Fixed or DriveType.Removable && drive.IsReady) rows.Add(new(drive.Name,drive.TotalSize,drive.AvailableFreeSpace)); }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException) { skipped++; }
        Volumes.ItemsSource=rows;
        VolumeNotes.Text=(rows.Any(v=>v.UsedPercent>=90)?"Hay una unidad con menos del 10% libre. Revisa archivos grandes y temporales antes de decidir qué borrar. ":"")+
            (skipped>0?$"{skipped} unidades no se pudieron consultar.":"");
    }
    private async Task Run(string label,Func<CancellationToken,Task> action)
    {
        if(operation is not null || closed) return;
        using var request=new CancellationTokenSource(); operation=request;
        Tabs.IsEnabled=false; RefreshButton.IsEnabled=false; CancelButton.IsEnabled=true; WorkStatus.Text=label;
        try { await action(request.Token); }
        catch(OperationCanceledException) { if(!closed) WorkStatus.Text="Operación cancelada."; }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or TimeoutException or System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException)
        { if(!closed) WorkStatus.Text=e is TimeoutException?e.Message:"No se pudo completar la operación. Revisa la ruta, los permisos o la disponibilidad de Windows. No se aplicaron reparaciones al sistema."; }
        finally
        {
            operation=null;
            if(!closed) { Tabs.IsEnabled=true; RefreshButton.IsEnabled=true; CancelButton.IsEnabled=false; CleanButton.IsEnabled=cleaning.Length>0; }
        }
    }
    private void CancelWork(object sender,RoutedEventArgs e) => operation?.Cancel();
    private async void RefreshHardware(object sender,RoutedEventArgs e) => await Run("Leyendo hardware local · hasta 35 segundos…",async token=>
    {
        var data=await SystemInventory.ReadAsync(token);
        if(closed) return;
        ShowInventory(data); RefreshVolumes(); WorkStatus.Text=$"Hardware actualizado a las {DateTime.Now:HH:mm:ss} · no se enviaron datos a la IA";
    });
    private void ShowInventory(SystemInventory data)
    {
        inventory=data; ComputerName.Text=string.IsNullOrWhiteSpace(data.Computer)?"Modelo no disponible":data.Computer;
        RamAdvice.Text=data.RamAdvice; RamModules.ItemsSource=data.Ram;
        DiskCards.ItemsSource=data.Disks; Parts.ItemsSource=data.Cpu.Concat(data.Board).Concat(data.Gpu).ToArray(); Issues.ItemsSource=data.Issues;
        DiskNotes.Text=(data.Disks.Length==0?"No se obtuvieron discos físicos. ":"")+string.Join("\n",data.Notes);
        ComponentNotes.Text=(data.Issues.Length==0?"La consulta no devolvió incidencias de dispositivos; esto no garantiza ausencia de fallos. ":"Consulta cada código en el Administrador de dispositivos y el soporte del fabricante antes de cambiar controladores. ")+string.Join("\n",data.Notes);
        var advice=new List<string> { "Inventario local: CPU, placa/BIOS, GPU, módulos RAM y discos. No se han hecho pruebas de esfuerzo ni reparaciones." };
        if(data.Disks.Any(d=>d.Health is "Warning" or "Unhealthy" || d.ReadErrors>0 || d.WriteErrors>0)) advice.Add("Windows informa una alerta o errores de almacenamiento. Prioriza una copia de seguridad y una revisión del disco/controlador; un contador aislado no determina la causa.");
        if(data.Disks.Any(d=>d.Wear is >=80 and <=100)) advice.Add("Hay un disco con desgaste reportado de al menos 80%. Mantén una copia de seguridad y consulta las especificaciones del fabricante; no es una estimación de años restantes.");
        if(data.Disks.Any(d=>d.Temperature is >=70 and <150)) advice.Add("Un disco informa al menos 70 °C. Comprueba la ventilación y compara con el límite de su fabricante; el umbral depende del modelo.");
        advice.Add($"Incidencias de dispositivos devueltas: {data.Issues.Length}. Revisa la pestaña Componentes y sus notas de disponibilidad.");
        advice.Add("RAM: consulta ubicaciones, formato y velocidad en su pestaña. BAEL no puede confirmar solo con SMBIOS si está soldada o qué módulo comprar.");
        advice.Add("Temperaturas de CPU/GPU, voltajes y estabilidad de la placa no se evalúan con esta lectura nativa.");
        Recommendations.Text=string.Join("\n\n",advice);
    }
    private void PickFolder(object sender,RoutedEventArgs e)
    {
        var dialog=new Microsoft.Win32.OpenFolderDialog { Title="Carpeta que quieres examinar" };
        if(dialog.ShowDialog(this)==true) SearchRoot.Text=dialog.FolderName;
    }
    private async void SearchFiles(object sender,RoutedEventArgs e)
    {
        string name=SearchName.Text.Trim();
        if(name.Length==0) { SearchReport.Text="Escribe una parte del nombre o pulsa Archivos grandes."; return; }
        await Search(name,0);
    }
    private async void FindLarge(object sender,RoutedEventArgs e) => await Search("",10*1024*1024);
    private async Task Search(string name,long minimum)
    {
        string root=SearchRoot.Text.Trim();
        FileResults.ItemsSource=null;
        await Run("Explorando nombres y tamaños de archivos…",async token=>
        {
            var progress=new Progress<string>(text=> { if(!closed && operation is not null) WorkStatus.Text=text; });
            var result=await Task.Run(()=>SystemFiles.Scan(root,name,minimum,token,progress),token);
            if(closed) return;
            FileResults.ItemsSource=result.Files;
            SearchReport.Text=$"{result.Files.Length:N0} resultados · {result.Visited:N0} elementos revisados · {result.Skipped:N0} omitidos."+
                (result.Limited?" Recorrido parcial por límite de tiempo o cantidad; elige una carpeta menor para continuar.":" Recorrido terminado; las zonas inaccesibles no se incluyeron.");
            WorkStatus.Text="Búsqueda terminada · no se abrió ni modificó ningún archivo.";
        });
    }
    private void RevealFile(object sender,RoutedEventArgs e)
    {
        if(FileResults.SelectedItem is not DiskFile file) { SearchReport.Text="Selecciona un resultado para mostrar su ubicación."; return; }
        try
        {
            var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")) { UseShellExecute=false };
            start.ArgumentList.Add("/select,"); start.ArgumentList.Add(file.Path); Process.Start(start);
        }
        catch(Exception error) when(error is System.ComponentModel.Win32Exception or InvalidOperationException) { SearchReport.Text="Windows no pudo mostrar la carpeta."; }
    }
    private async void AnalyzeCleanup(object sender,RoutedEventArgs e)
    {
        cleaning=[]; CleanFiles.ItemsSource=null; CleanButton.IsEnabled=false;
        await Run("Analizando temporales antiguos del usuario…",async token=>
        {
            var result=await Task.Run(()=>SystemFiles.Scan(temporaryRoot,"",0,token,temporary:true),token);
            if(closed) return;
            cleaning=result.Files; CleanFiles.ItemsSource=cleaning;
            CleanReport.Text=$"{cleaning.Length:N0} candidatos · hasta {SystemMetrics.Size(cleaning.Sum(f=>f.Bytes))}. La limpieza vuelve a verificar edad, ruta y uso."+
                (result.Limited?" Análisis parcial: puedes limpiar esta selección y repetirlo.":"");
            WorkStatus.Text="Vista previa lista. Revisa la selección antes de limpiar.";
        });
    }
    private void SelectAllCleanup(object sender,RoutedEventArgs e) { foreach(var file in cleaning) file.Selected=true; CleanFiles.Items.Refresh(); }
    private void ClearCleanup(object sender,RoutedEventArgs e) { foreach(var file in cleaning) file.Selected=false; CleanFiles.Items.Refresh(); }
    private async void CleanSelected(object sender,RoutedEventArgs e)
    {
        CleanFiles.CommitEdit(DataGridEditingUnit.Cell,true); CleanFiles.CommitEdit(DataGridEditingUnit.Row,true);
        var selected=cleaning.Where(f=>f.Selected).ToArray();
        if(selected.Length==0) { CleanReport.Text="Selecciona al menos un temporal de la vista previa."; return; }
        await Run("Limpiando únicamente los temporales seleccionados…",async token=>
        {
            var result=await Task.Run(()=>SystemFiles.Clean(temporaryRoot,selected,token),token);
            if(closed) return;
            cleaning=[]; CleanFiles.ItemsSource=null; RefreshVolumes();
            CleanReport.Text=$"Eliminados {result.Deleted:N0} archivos · {SystemMetrics.Size(result.Bytes)} en tamaños de archivo retirados · {result.Skipped:N0} omitidos."+
                (result.Cancelled?" Limpieza interrumpida; los cambios ya realizados se conservan.":"")+" El espacio libre real puede diferir por compresión o instantáneas; se actualizó en Resumen.";
            WorkStatus.Text="Limpieza terminada. Puedes analizar otra vez para actualizar la lista.";
        });
    }
    internal void VerifyInterface()
    {
        ShowInventory(new() { Computer="PC de prueba",Ram=[new("DIMM 0",8L<<30,26,8,3200,2666,"Fabricante","Referencia")],Arrays=[new(2,16L<<20)],
            Disks=[new("Disco de prueba","SSD","NVMe",256L<<30,"Warning","OK",null,85,1000,1,0)],Cpu=[new("CPU de prueba","8 hilos","OK")],Issues=[new("Dispositivo de prueba",28)] });
        if(!RamAdvice.Text.Contains("Posibles") || !Recommendations.Text.Contains("copia de seguridad") || DiskCards.Items.Count!=1 || Parts.Items.Count!=1 || Issues.Items.Count!=1 || CleanButton.IsEnabled)
            throw new InvalidOperationException("El centro de sistema perdió datos, límites o recomendaciones.");
        SelectSection(4); if(Tabs.SelectedIndex!=4) throw new InvalidOperationException("No abrió la limpieza integrada.");
    }
}
