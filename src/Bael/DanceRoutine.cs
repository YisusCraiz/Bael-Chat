namespace Bael;

internal enum DanceStage { None, Dress, Dancing, Undress }
internal readonly record struct DancePose(double X, double Y, double Angle, double ScaleX, double ScaleY, int Frame);

internal sealed class DanceRoutine
{
    internal DanceStage Stage { get; private set; }
    internal long Started { get; private set; }
    internal bool Active => Stage != DanceStage.None;
    internal DanceStage Update(bool sound, long now)
    {
        if (Stage == DanceStage.None && sound) Enter(DanceStage.Dress, now);
        else if (Stage == DanceStage.Dress && now-Started >= 800) Enter(sound ? DanceStage.Dancing : DanceStage.Undress,now);
        else if (Stage == DanceStage.Dancing && !sound) Enter(DanceStage.Undress,now);
        else if (Stage == DanceStage.Undress && now-Started >= 640) Enter(sound ? DanceStage.Dress : DanceStage.None,now);
        return Stage;
    }
    private void Enter(DanceStage stage, long now) { Stage=stage; Started=now; }
    internal void Reset() { Stage=DanceStage.None; Started=0; }
    internal int MagicFrame(long now) => Stage == DanceStage.Undress
        ? 3-Math.Clamp((int)((now-Started)/160),0,3) : Math.Clamp((int)((now-Started)/200),0,3);
    internal static DancePose Pose(double seconds)
    {
        double time=Math.Max(0,seconds)%16;
        int move=(int)(time/2);
        double t=(time%2)/2;
        double beat=seconds*Math.PI*4;
        double hop=Math.Pow(Math.Sin(Math.PI*t),2);
        return move switch
        {
            0 => new(Math.Sin(beat)*8,-Math.Abs(Math.Sin(beat))*5,Math.Sin(beat)*6,1,1, (int)(t*8)%2), // zapateo
            1 => new(Math.Sin(beat/2)*9,-3,Math.Sin(beat/2)*14,1,1,2), // disco
            2 => new(0,-hop*18,t*360,1,1,5), // vuelta completa
            3 => new(Math.Sin(beat/2)*10,-Math.Abs(Math.Sin(beat))*7,Math.Sin(beat/2)*9,1,1,3), // pasos cruzados
            4 => new(0,-hop*32,0,1,1-0.10*Math.Cos(beat),4), // salto
            5 => new(Math.Sin(Math.PI*2*t)*10,-hop*26,t*360,0.84,0.84,t<0.2?6:7), // marometa
            6 => new(Math.Sin(beat/2)*12,-Math.Abs(Math.Sin(beat))*9,Math.Sin(beat/2)*18,Math.Cos(t*Math.PI*2),1,5), // giro sobre sí mismo
            _ => new(Math.Sin(beat)*5,-Math.Abs(Math.Sin(beat))*12,Math.Sin(beat)*5,1,1,7)
        };
    }
    internal static void Verify()
    {
        var routine=new DanceRoutine();
        if(routine.Update(true,0)!=DanceStage.Dress || routine.Update(true,800)!=DanceStage.Dancing
            || routine.Update(false,1200)!=DanceStage.Undress || routine.Update(false,1840)!=DanceStage.None)
            throw new InvalidOperationException("El disfraz de baile no completó sus transiciones.");
        for(int i=0;i<1600;i++)
        {
            var p=Pose(i/100.0);
            if(!double.IsFinite(p.Angle) || Math.Abs(p.X)>12 || p.Y < -33 || p.Frame is <0 or >7)
                throw new InvalidOperationException("Una pose de baile salió del área reservada.");
        }
    }
}
