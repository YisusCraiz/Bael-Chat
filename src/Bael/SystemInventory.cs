using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Bael;

internal sealed record HardwareDisk(string Name,string Media,string Bus,long Bytes,string Health,string Operation,double? Temperature,double? Wear,long? Hours,long? ReadErrors,long? WriteErrors)
{
    public string Manufacturer {get;init;}="";
    public string Model {get;init;}="";
    public string Serial {get;init;}="";
    public string Firmware {get;init;}="";
    public string Identity=>$"Fabricante: {Known(Manufacturer)} · Modelo: {Known(Model)}\nSerie: {Known(Serial)} · Firmware: {Known(Firmware)}";
    private static string Known(string value)=>string.IsNullOrWhiteSpace(value)?"No informado":value.Trim();
    public string Size=>SystemMetrics.Size(Bytes);
    public string State=>string.IsNullOrWhiteSpace(Health)?"No disponible":Health;
    public string TemperatureText=>Temperature is >0 and <150?$"{Temperature:0} °C":"No disponible";
    public string WearText=>Wear is >=0 and <=100?$"{Wear:0}% consumido (reportado)":"No disponible";
    public string HoursText=>Hours is >=0?$"{Hours:N0} h":"No disponible";
    public string ErrorText=>$"Lectura: {(ReadErrors?.ToString()??"N/D")} · escritura: {(WriteErrors?.ToString()??"N/D")}";
}
internal sealed record RamStick(string Slot,long Bytes,int Type,int Form,int Speed,int Configured,string Maker,string Part)
{
    public string Serial {get;init;}="";
    public string Bank {get;init;}="";
    public double? Voltage {get;init;}
    public string Identity=>$"Fabricante: {(string.IsNullOrWhiteSpace(Maker)?"No informado":Maker.Trim())} · Referencia: {(string.IsNullOrWhiteSpace(Part)?"No informada":Part.Trim())}\nBanco: {(string.IsNullOrWhiteSpace(Bank)?"No informado":Bank)} · Serie: {(string.IsNullOrWhiteSpace(Serial)?"No informada":Serial)}";
    public string VoltageText=>Voltage is >0?$"{Voltage:0.###} V (configurada, informada por firmware)":"Voltaje no informado";
    public string Size=>SystemMetrics.Size(Bytes);
    public string Kind=>Type switch { 26=>"DDR4",34=>"DDR5",24=>"DDR3",30=>"LPDDR4",35=>"LPDDR5",_=>$"SMBIOS {Type} (no identificado)" };
    public string Format=>Form switch {8=>"DIMM",12=>"SO-DIMM",_=>"No confirmado"};
    public string Frequency=>$"Configurada (WMI): {(Configured>0?Configured.ToString():"N/D")} MHz · Speed nominal: {(Speed>0?Speed.ToString():"N/D")} (valor bruto)";
}
internal sealed record RamArray(int Slots,long MaximumKb);
internal sealed record HardwarePart(string Name,string Detail,string Status);
internal sealed record HardwareIssue(string Name,int Code);
internal sealed record SystemInventory
{
    public string WindowsUser {get;init;}="No informado";
    public string Computer {get;init;}="No disponible";
    public HardwarePart[] Cpu {get;init;}=[];
    public HardwarePart[] Board {get;init;}=[];
    public HardwarePart[] Gpu {get;init;}=[];
    public RamStick[] Ram {get;init;}=[];
    public RamArray[] Arrays {get;init;}=[];
    public HardwareDisk[] Disks {get;init;}=[];
    public HardwareIssue[] Issues {get;init;}=[];
    public string[] Notes {get;init;}=[];
    internal string RamAdvice
    {
        get
        {
            int slots=Arrays.Sum(a=>a.Slots),used=Ram.Length; long max=Arrays.Sum(a=>a.MaximumKb)*1024,installed=Ram.Sum(r=>r.Bytes);
            string detail=$"{used} módulos detectados · {(slots>0?slots.ToString():"N/D")} ubicaciones declaradas por firmware.\n"+
                (slots>used?$"Posibles ubicaciones sin ocupar: {slots-used}. No confirma que sean ranuras accesibles ni que la RAM sea reemplazable.\n":"No se ha confirmado una ranura libre. Puede requerir reemplazar módulos o tener RAM soldada.\n")+
                (max>0?$"Máximo declarado por SMBIOS: {SystemMetrics.Size(max)}; instalado: {SystemMetrics.Size(installed)}.\n":"El firmware no declaró una capacidad máxima fiable.\n");
            if(slots<used || max>0 && max<installed) detail+="Los datos del firmware son inconsistentes; no sirven para decidir una compra.\n";
            return detail+"Antes de ampliar: consulta el manual del modelo exacto para confirmar RAM soldada, ranuras, máximo, DDR, formato y velocidades admitidas. La velocidad detectada describe lo instalado; no garantiza una ampliación compatible. Una lectura alta de uso es una muestra, no un diagnóstico de RAM insuficiente.";
        }
    }
    internal static async Task<SystemInventory> ReadAsync(CancellationToken token)
    {
        string executable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe");
        var start=new ProcessStartInfo(executable) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 };
        start.ArgumentList.Add("-NoLogo"); start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-EncodedCommand"); start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(Script)));
        using var process=new Process { StartInfo=start };
        if(!process.Start()) throw new IOException("Windows no pudo iniciar la lectura de hardware.");
        Task<string> output=process.StandardOutput.ReadToEndAsync(),error=process.StandardError.ReadToEndAsync();
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(token); limit.CancelAfter(TimeSpan.FromSeconds(35));
        try { await process.WaitForExitAsync(limit.Token); }
        catch(OperationCanceledException)
        {
            try { process.Kill(entireProcessTree:true); } catch(InvalidOperationException) { }
            await Task.WhenAll(output,error);
            token.ThrowIfCancellationRequested(); throw new TimeoutException("La lectura de hardware tardó demasiado. Puedes volver a intentarlo.");
        }
        string text=await output; await error;
        if(process.ExitCode!=0 || string.IsNullOrWhiteSpace(text)) throw new IOException("La lectura local fue bloqueada o no está disponible. No se cambió la configuración del equipo.");
        return JsonSerializer.Deserialize<SystemInventory>(text.Trim().TrimStart('\uFEFF'),new JsonSerializerOptions { PropertyNameCaseInsensitive=true })
            ?? throw new JsonException("Windows no devolvió información legible.");
    }
    internal static async Task VerifySyntaxAsync()
    {
        string check="$t=$null;$e=$null;[System.Management.Automation.Language.Parser]::ParseInput([Console]::In.ReadToEnd(),[ref]$t,[ref]$e)|Out-Null;if($e.Count -gt 0){exit 1};Write-Output 'OK'";
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe"))
            { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(check)));
        using var process=Process.Start(start)!;
        var output=process.StandardOutput.ReadToEndAsync(); var errors=process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(Script); process.StandardInput.Close();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch(OperationCanceledException) { process.Kill(true); throw new InvalidOperationException("No terminó la validación del lector local."); }
        string result=await output; await errors;
        if(process.ExitCode!=0 || result.Trim()!="OK") throw new InvalidOperationException("El lector de hardware tiene sintaxis no válida.");
    }

    // Fixed read-only commands. Neither file searches nor user messages are interpolated here.
    private const string Script="""
    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $ErrorActionPreference='Stop'
    $notes = New-Object 'System.Collections.Generic.List[string]'
    function Read-Cim($class) {
        try { @(Get-CimInstance -ClassName $class -OperationTimeoutSec 5 -ErrorAction Stop) }
        catch { $notes.Add("No disponible: $class (permisos, firmware o proveedor).") }
    }
    $computer=@(Read-Cim 'Win32_ComputerSystem')
    $cpu=@(Read-Cim 'Win32_Processor' | ForEach-Object { @{Name=[string]$_.Name;Detail="Núcleos: $($_.NumberOfCores) · hilos: $($_.NumberOfLogicalProcessors) · máximo informado: $($_.MaxClockSpeed) MHz";Status=[string]$_.Status} })
    $board=@(Read-Cim 'Win32_BaseBoard' | ForEach-Object { @{Name="$($_.Manufacturer) $($_.Product)";Detail="Versión: $($_.Version) · estado WMI (no prueba física)";Status=[string]$_.Status} })
    $bios=@(Read-Cim 'Win32_BIOS' | ForEach-Object { @{Name='BIOS / UEFI';Detail="$($_.Manufacturer) · $($_.SMBIOSBIOSVersion)";Status=[string]$_.Status} })
    $gpu=@(Read-Cim 'Win32_VideoController' | ForEach-Object { @{Name=[string]$_.Name;Detail="Controlador: $($_.DriverVersion)";Status=[string]$_.Status} })
    $ram=@(Read-Cim 'Win32_PhysicalMemory' | ForEach-Object { @{Slot=[string]$_.DeviceLocator;Bytes=[long]$_.Capacity;Type=[int]$_.SMBIOSMemoryType;Form=[int]$_.FormFactor;Speed=[int]$_.Speed;Configured=[int]$_.ConfiguredClockSpeed;Maker=[string]$_.Manufacturer;Part=([string]$_.PartNumber).Trim();Serial=([string]$_.SerialNumber).Trim();Bank=[string]$_.BankLabel;Voltage=$(if($_.ConfiguredVoltage -gt 0){[double]$_.ConfiguredVoltage/1000}else{$null})} })
    $arrays=@(Read-Cim 'Win32_PhysicalMemoryArray' | Where-Object { $_.Use -eq 3 } | ForEach-Object { @{Slots=[int]$_.MemoryDevices;MaximumKb=[long]$_.MaxCapacityEx} })
    $issues=@()
    try { $issues=@(Get-CimInstance -ClassName Win32_PnPEntity -Filter 'ConfigManagerErrorCode <> 0' -OperationTimeoutSec 5 | Select-Object -First 100 | ForEach-Object { @{Name=[string]$_.Name;Code=[int]$_.ConfigManagerErrorCode} }) }
    catch { $notes.Add('No se pudo consultar el estado de los dispositivos Plug and Play.') }
    $legacyDisks=@(Read-Cim 'Win32_DiskDrive')
    $disks=@()
    try {
        $disks=@(Get-PhysicalDisk -ErrorAction Stop | ForEach-Object {
            $d=$_; $r=$null
            $serial=([string]$d.SerialNumber).Trim()
            $matches=@($legacyDisks | Where-Object { $serial -and ([string]$_.SerialNumber).Trim() -eq $serial })
            $matched=$null
            if($matches.Count -eq 1){$matched=$matches[0]}
            $maker=([string]$d.Manufacturer).Trim()
            if(-not $maker -and $null -ne $matched){$maker=([string]$matched.Manufacturer).Trim()}
            $model=([string]$d.Model).Trim()
            if(-not $model -and $null -ne $matched){$model=([string]$matched.Model).Trim()}
            if(-not $model){$model=[string]$d.FriendlyName}
            $firmware=([string]$d.FirmwareVersion).Trim()
            if(-not $firmware -and $null -ne $matched){$firmware=([string]$matched.FirmwareRevision).Trim()}
            try { $r=$d | Get-StorageReliabilityCounter -ErrorAction Stop } catch { $notes.Add("Sin contadores de fiabilidad para $($d.FriendlyName): puede requerir permisos o no estar soportado.") }
            @{Manufacturer=$maker;Model=$model;Serial=$serial;Firmware=$firmware;Name=[string]$d.FriendlyName;Media=[string]$d.MediaType;Bus=[string]$d.BusType;Bytes=[long]$d.Size;Health=[string]$d.HealthStatus;Operation=($d.OperationalStatus -join ', ');Temperature=$r.Temperature;Wear=$r.Wear;Hours=$r.PowerOnHours;ReadErrors=$r.ReadErrorsUncorrected;WriteErrors=$r.WriteErrorsUncorrected}
        })
    } catch { $notes.Add('El proveedor de almacenamiento no expuso los discos físicos. Las unidades y su espacio siguen disponibles.') }
    if($disks.Count -eq 0 -and $legacyDisks.Count -gt 0){
        $notes.Add('Identidad de discos obtenida por Win32_DiskDrive; fiabilidad no disponible por este proveedor.')
        $disks=@($legacyDisks | ForEach-Object { @{Name=[string]$_.Model;Model=[string]$_.Model;Manufacturer=[string]$_.Manufacturer;Serial=([string]$_.SerialNumber).Trim();Firmware=[string]$_.FirmwareRevision;Media=[string]$_.MediaType;Bus=[string]$_.InterfaceType;Bytes=[long]$_.Size;Health='No disponible';Operation=[string]$_.Status;Temperature=$null;Wear=$null;Hours=$null;ReadErrors=$null;WriteErrors=$null} })
    }
    @{WindowsUser=(($computer | ForEach-Object { [string]$_.UserName }) -join ', ');Computer=(($computer | ForEach-Object { "$($_.Manufacturer) $($_.Model)" }) -join ', ');Cpu=$cpu;Board=@($board)+@($bios);Gpu=$gpu;Ram=$ram;Arrays=$arrays;Issues=$issues;Disks=$disks;Notes=@($notes.ToArray())} | ConvertTo-Json -Depth 6 -Compress
    """;
}
