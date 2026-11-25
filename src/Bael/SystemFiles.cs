using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Bael;

internal sealed record DiskFile(string Path,long Bytes,DateTime Modified)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    public string Size => SystemMetrics.Size(Bytes);
    public bool Selected { get; set; }=true;
}
internal sealed record FileScan(DiskFile[] Files,int Visited,int Skipped,bool Limited);
internal sealed record CleanResult(int Deleted,long Bytes,int Skipped,bool Cancelled);

internal static class SystemFiles
{
    internal static bool SafeWithin(string root,string path)
    {
        root=System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
        string full=System.IO.Path.GetFullPath(path);
        if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)) return false;
        string? current=full;
        try
        {
            while(current is not null && current.Length>=root.Length-1)
            {
                if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) return false;
                current=System.IO.Path.GetDirectoryName(current);
            }
            return true;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException) { return false; }
    }
    internal static FileScan Scan(string root,string name,long minimum,CancellationToken token,IProgress<string>? progress=null, bool temporary=false,DateTime? clock=null)
    {
        root=System.IO.Path.GetFullPath(root);
        if(!Directory.Exists(root)) throw new IOException("La carpeta no existe o no está disponible.");
        if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0) throw new IOException("Elige una carpeta directa, no un enlace o unión.");
        var pending=new Stack<string>(); pending.Push(root);
        var files=new List<DiskFile>(); int visited=0,skipped=0; bool limited=false;
        DateTime now=clock ?? DateTime.UtcNow; var watch=System.Diagnostics.Stopwatch.StartNew();
        while(pending.Count>0)
        {
            token.ThrowIfCancellationRequested();
            if(visited>=200000 || watch.Elapsed>TimeSpan.FromSeconds(30)) { limited=true; break; }
            string folder=pending.Pop();
            try
            {
                foreach(string entry in Directory.EnumerateFileSystemEntries(folder))
                {
                    token.ThrowIfCancellationRequested();
                    if(++visited>=200000 || watch.Elapsed>TimeSpan.FromSeconds(30)) { limited=true; break; }
                    try
                    {
                        FileAttributes attr=File.GetAttributes(entry);
                        if((attr&FileAttributes.ReparsePoint)!=0) { skipped++; continue; }
                        if((attr&FileAttributes.Directory)!=0)
                        {
                            if(!temporary || !System.IO.Path.GetFileName(entry).StartsWith("BAEL",StringComparison.OrdinalIgnoreCase)) pending.Push(entry);
                            continue;
                        }
                        if(temporary && (attr&(FileAttributes.System|FileAttributes.ReadOnly))!=0) { skipped++; continue; }
                        var info=new FileInfo(entry);
                        if(!info.Name.Contains(name,StringComparison.OrdinalIgnoreCase) || info.Length<minimum) continue;
                        if(temporary && (info.LastWriteTimeUtc>now.AddDays(-7) || info.LastAccessTimeUtc>now.AddDays(-7)
                            || info.Name.StartsWith("BAEL",StringComparison.OrdinalIgnoreCase))) continue;
                        files.Add(new(entry,info.Length,info.LastWriteTimeUtc));
                        if(temporary && files.Count>=5000 || !temporary && name.Length>0 && files.Count>=500)
                        { limited=true; pending.Clear(); break; }
                        if(!temporary && name.Length==0 && files.Count>300) files=files.OrderByDescending(f=>f.Bytes).Take(200).ToList();
                    }
                    catch(Exception e) when(e is IOException or UnauthorizedAccessException) { skipped++; }
                    if(visited%500==0) progress?.Report($"Revisados {visited:N0} elementos · {files.Count:N0} coincidencias · {skipped:N0} omitidos");
                }
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException) { skipped++; }
            if(limited) break;
        }
        return new(files.OrderByDescending(f=>f.Bytes).Take(temporary?5000:name.Length>0?500:200).ToArray(),visited,skipped,limited);
    }
    internal static CleanResult Clean(string root,IEnumerable<DiskFile> selection,CancellationToken token,DateTime? clock=null)
    {
        DateTime cutoff=(clock??DateTime.UtcNow).AddDays(-7); int deleted=0,skipped=0; long bytes=0; bool cancelled=false;
        foreach(var file in selection.Where(f=>f.Selected))
        {
            if(token.IsCancellationRequested) { cancelled=true; break; }
            try
            {
                if(!SafeWithin(root,file.Path)) { skipped++; continue; }
                var info=new FileInfo(file.Path);
                if(!info.Exists || info.Length!=file.Bytes || info.LastWriteTimeUtc!=file.Modified
                    || info.LastWriteTimeUtc>cutoff || info.LastAccessTimeUtc>cutoff
                    || (info.Attributes&(FileAttributes.ReadOnly|FileAttributes.System|FileAttributes.Directory))!=0
                    || System.IO.Path.GetRelativePath(root,file.Path).Split(System.IO.Path.DirectorySeparatorChar)
                        .Any(part=>part.StartsWith("BAEL",StringComparison.OrdinalIgnoreCase))) { skipped++; continue; }
                if(!RemoveChecked(root,file,cutoff)) { skipped++; continue; }
                bytes+=file.Bytes; deleted++;
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException) { skipped++; }
        }
        return new(deleted,bytes,skipped,cancelled);
    }
    [StructLayout(LayoutKind.Sequential)] private struct FileClock { public uint Low,High; public long Value=>(long)(((ulong)High<<32)|Low); }
    [StructLayout(LayoutKind.Sequential)] private struct FileDetails
    {
        public uint Attributes; public FileClock Created,Accessed,Written;
        public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
        public long Bytes=>(long)(((ulong)SizeHigh<<32)|SizeLow);
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern SafeFileHandle CreateFile(string path,uint access,uint share,nint security,uint disposition,uint flags,nint template);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle file,out FileDetails info);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern uint GetFinalPathNameByHandle(SafeFileHandle file,StringBuilder path,uint length,uint flags);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileInformationByHandle(SafeFileHandle file,int kind,ref byte disposition,uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern uint GetLongPathName(string shortName,StringBuilder longName,uint length);
    private static string LongPath(string value)
    {
        var result=new StringBuilder(32768); uint length=GetLongPathName(value,result,(uint)result.Capacity);
        return length>0 && length<result.Capacity?result.ToString():value;
    }
    private static bool RemoveChecked(string root,DiskFile expected,DateTime cutoff)
    {
        // OPEN_EXISTING, exclusive READ + DELETE access, and OPEN_REPARSE_POINT.
        // No deletion is requested until the opened file passes the checks below.
        using var handle=CreateFile(expected.Path,0x80010000,0,0,3,0x00200000,0);
        if(handle.IsInvalid || !GetFileInformationByHandle(handle,out var info)) return false;
        if(info.Links!=1 || info.Bytes!=expected.Bytes || DateTime.FromFileTimeUtc(info.Written.Value)!=expected.Modified
            || DateTime.FromFileTimeUtc(info.Written.Value)>cutoff
            || (info.Attributes&(uint)(FileAttributes.ReparsePoint|FileAttributes.Directory|FileAttributes.ReadOnly|FileAttributes.System))!=0) return false;
        var path=new StringBuilder(32768);
        uint length=GetFinalPathNameByHandle(handle,path,(uint)path.Capacity,0);
        if(length==0 || length>=path.Capacity) return false;
        string actual=path.ToString(); if(actual.StartsWith(@"\\?\")) actual=actual[4..];
        string prefix=LongPath(System.IO.Path.GetFullPath(root)).TrimEnd('\\')+"\\";
        if(!actual.StartsWith(prefix,StringComparison.OrdinalIgnoreCase) || !string.Equals(actual,LongPath(System.IO.Path.GetFullPath(expected.Path)),StringComparison.OrdinalIgnoreCase)) return false;
        byte delete=1;
        return SetFileInformationByHandle(handle,4,ref delete,1);
    }

}
