namespace Bael;

internal readonly record struct SixSevenPose(int Frame,double SixY,double SevenY,double Sway,double Bob);
internal static class SixSevenRoutine
{
    private static readonly double[] Left=[66,84,108,108,84,66];
    private static readonly double[] Right=[108,84,66,66,84,108];
    internal static SixSevenPose Pose(double seconds)
    {
        double phase=Math.Max(0,seconds)/0.18;int frame=(int)(phase%6),next=(frame+1)%6;
        double fraction=phase-Math.Floor(phase),ease=fraction*fraction*(3-2*fraction);
        return new(frame,Left[frame]+(Left[next]-Left[frame])*ease,Right[frame]+(Right[next]-Right[frame])*ease,Math.Sin(seconds*Math.PI*2/1.08)*8,-Math.Abs(Math.Sin(seconds*Math.PI*2/1.08))*3);
    }
    internal static void Verify()
    {
        var first=Pose(0);var opposite=Pose(.36);var loop=Pose(1.08);
        if(first.Frame!=0 || opposite.Frame!=2 || first.SixY>=first.SevenY || opposite.SixY<=opposite.SevenY || loop.Frame!=0) throw new InvalidOperationException("El baile perdió la alternancia o el bucle.");
        for(int i=0;i<120;i++) { var pose=Pose(i/60.0);if(pose.Frame is <0 or >5 || !double.IsFinite(pose.SixY) || !double.IsFinite(pose.SevenY)) throw new InvalidOperationException("La animación generó una pose inválida."); }
    }
}
