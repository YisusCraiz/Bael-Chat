using System.IO;
using System.Text.Json;

namespace Bael;

internal static class SystemChecks
{
    internal static async Task RunAsync()
    {
        var unknown=new HardwareDisk("Disco","SSD","USB",100,"Unknown","",null,null,null,null,null);
        if(unknown.TemperatureText!="No disponible" || unknown.WearText!="No disponible" || !unknown.ErrorText.Contains("N/D")) Fail("Se inventó salud de un disco sin contadores.");
        var hardware=new SystemInventory { Ram=[new("DIMM",8L<<30,26,8,3200,2666,"Fabricante","Parte")],Arrays=[new(2,16L<<20)] };
        if(!hardware.RamAdvice.Contains("no garantiza") || !hardware.RamAdvice.Contains("No confirma")) Fail("La recomendación de RAM prometió compatibilidad.");
        var restored=JsonSerializer.Deserialize<SystemInventory>(JsonSerializer.Serialize(hardware))!;
        if(restored.Ram.Length!=1 || restored.Ram[0].Configured!=2666 || !restored.Ram[0].Frequency.Contains("3200")) Fail("Se perdieron los datos de RAM.");
        if(SystemCommands.Section("liberar espacio")!=4 || SystemCommands.Section("no abras el panel del sistema") is not null) Fail("Los comandos técnicos ejecutaron una negación.");
        var window=new SystemWindow(new BaelSettings());
        try { window.VerifyInterface(); } finally { window.Close(); }
        var live=new SystemMetrics(); live.Read(); var sample=live.Read();
        if(sample.Cpu is <0 or >100 || sample.Ram is <0 or >100) Fail("Los indicadores devolvieron porcentajes imposibles.");
        string root=Path.Combine(Path.GetTempPath(),"system-check-"+Guid.NewGuid().ToString("N"));
        string outside=root+"-outside"; Directory.CreateDirectory(root); Directory.CreateDirectory(outside);
        try
        {
            DateTime now=DateTime.UtcNow;
            DiskFile Old(string path,string text="old")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path,text);
                File.SetLastWriteTimeUtc(path,now.AddDays(-10)); File.SetLastAccessTimeUtc(path,now.AddDays(-10));
                var info=new FileInfo(path); return new(path,info.Length,info.LastWriteTimeUtc);
            }
            var old=Old(Path.Combine(root,"old.tmp")); var changed=Old(Path.Combine(root,"changed.tmp"));
            var locked=Old(Path.Combine(root,"locked.tmp")); var notSelected=Old(Path.Combine(root,"not-selected.tmp")); notSelected.Selected=false;
            var external=Old(Path.Combine(outside,"outside.tmp")); var protectedFile=Old(Path.Combine(root,"readonly.tmp"));
            File.SetAttributes(protectedFile.Path,FileAttributes.ReadOnly);
            var bael=Old(Path.Combine(root,"BAEL-session","retained.tmp"));
            string recent=Path.Combine(root,"recent.tmp"); File.WriteAllText(recent,"recent");
            var scan=SystemFiles.Scan(root,"",0,CancellationToken.None,temporary:true,clock:now);
            if(!scan.Files.Any(f=>f.Path==old.Path) || scan.Files.Any(f=>f.Path==recent || f.Path==protectedFile.Path || f.Path==bael.Path)) Fail("El análisis incluyó temporales no aptos.");
            if(SystemFiles.SafeWithin(root,external.Path) || SystemFiles.SafeWithin(root,root)) Fail("La limpieza permitió una ruta fuera de su carpeta.");
            File.AppendAllText(changed.Path,"new bytes");
            using(var hold=new FileStream(locked.Path,FileMode.Open,FileAccess.Read,FileShare.None))
            {
                var result=SystemFiles.Clean(root,[old,changed,locked,external,notSelected,protectedFile,bael],CancellationToken.None,now);
                if(result.Deleted!=1 || result.Bytes!=old.Bytes || File.Exists(old.Path) || !File.Exists(locked.Path) || !File.Exists(changed.Path) || !File.Exists(external.Path)
                    || !File.Exists(notSelected.Path) || !File.Exists(protectedFile.Path) || !File.Exists(bael.Path)) Fail("La limpieza salió de la selección válida o no eliminó el temporal verificado.");
            }
            using var canceled=new CancellationTokenSource(); canceled.Cancel();
            notSelected.Selected=true;
            if(!SystemFiles.Clean(root,[notSelected],canceled.Token,now).Cancelled || !File.Exists(notSelected.Path)) Fail("La limpieza ignoró la cancelación.");
            try { SystemFiles.Scan(root,"old",0,canceled.Token); Fail("La búsqueda ignoró la cancelación."); } catch(OperationCanceledException) { }
            bool link=false;
            try { Directory.CreateSymbolicLink(Path.Combine(root,"linked"),outside); link=true; } catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
            if(link && SystemFiles.Scan(root,"outside",0,CancellationToken.None).Files.Length!=0) Fail("La búsqueda siguió un enlace fuera de la carpeta.");
        }
        finally
        {
            foreach(string path in Directory.EnumerateFiles(root,"*",SearchOption.TopDirectoryOnly)) File.SetAttributes(path,FileAttributes.Normal);
            string link=Path.Combine(root,"linked"); if(Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(root,true); Directory.Delete(outside,true);
        }
        await SystemInventory.VerifySyntaxAsync();
    }
    private static void Fail(string text) => throw new InvalidOperationException(text);
}
