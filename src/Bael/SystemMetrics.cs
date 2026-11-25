using System.Runtime.InteropServices;

namespace Bael;

internal sealed record LiveMetrics(double? Cpu,double? Ram,long TotalRam,long FreeRam);
internal sealed class SystemMetrics
{
    private ulong previousIdle,previousKernel,previousUser;
    [StructLayout(LayoutKind.Sequential)] private struct Clock { public uint Low,High; public ulong Value=>((ulong)High<<32)|Low; }
    [StructLayout(LayoutKind.Sequential)] private sealed class Memory
    {
        public uint Length=(uint)Marshal.SizeOf<Memory>(); public uint Load;
        public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,AvailableExtended;
    }
    [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemTimes(out Clock idle,out Clock kernel,out Clock user);
    [DllImport("kernel32.dll",CharSet=CharSet.Auto)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx([In,Out] Memory status);
    internal LiveMetrics Read()
    {
        double? cpu=null; var memory=new Memory(); bool known=GlobalMemoryStatusEx(memory);
        if(GetSystemTimes(out var idle,out var kernel,out var user))
        {
            ulong total=kernel.Value+user.Value,previous=previousKernel+previousUser;
            if(previous>0 && total>previous && idle.Value>=previousIdle)
                cpu=Math.Clamp(100.0*(1-(idle.Value-previousIdle)/(double)(total-previous)),0,100);
            previousIdle=idle.Value; previousKernel=kernel.Value; previousUser=user.Value;
        }
        return new(cpu,known?memory.Load:null,known?(long)memory.TotalPhysical:0,known?(long)memory.AvailablePhysical:0);
    }
    internal static string Size(long bytes)
    {
        string[] units=["B","KB","MB","GB","TB"]; double value=Math.Max(0,bytes); int unit=0;
        while(value>=1024 && unit<4) { value/=1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}
