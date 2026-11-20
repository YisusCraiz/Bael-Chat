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
    private static readonly int[] Order = Enumerable.Range(0,14).OrderBy(_ => Random.Shared.Next()).ToArray();
    internal static DancePose Pose(double seconds)
    {
        double time=Math.Max(0,seconds)%28;
        int move=Order[((int)(time/2) + (int)(Math.Max(0,seconds)/28)*5)%14];
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
            5 => new(Math.Sin(Math.PI*2*t)*10,-hop*26,t*360,1-0.16*hop,1-0.16*hop,t<0.2?6:7), // marometa
            6 => new(Math.Sin(beat/2)*12,-Math.Abs(Math.Sin(beat))*9,Math.Sin(beat/2)*18,Math.Cos(t*Math.PI*2),1,5), // giro sobre sí mismo
            8 => new(12-24*t, -Math.Abs(Math.Sin(beat))*3, -10, 1, 1, 8+(int)(t*8)%2), // moonwalk
            9 => new(Math.Round(Math.Sin(beat))*5,-2,Math.Round(Math.Sin(beat))*6,1,1,10+(int)(t*4)%2), // robot
            10 => new(Math.Sin(beat/2)*5,-Math.Abs(Math.Sin(beat))*4,Math.Sin(beat/2)*14,1,1,12+(int)(t*4)%2), // dab
            11 => new(Math.Sin(beat)*7,-Math.Abs(Math.Sin(beat))*13,Math.Sin(beat)*4,1,1,14+(int)(t*8)%2), // running man
            12 => new(Math.Sin(beat/2)*12,-Math.Abs(Math.Sin(beat))*4,Math.Sin(beat/2)*10,1,1,16+(int)(t*4)%2), // salsa
            13 => new(Math.Sin(beat/2)*10,-hop*12,t*360,1-0.12*hop,1-0.12*hop,t<0.65?18:19), // breakdance
            _ => new(Math.Sin(beat)*5,-Math.Abs(Math.Sin(beat))*12,Math.Sin(beat)*5,1,1,7)
        };
    }
    internal static void Verify()
    {
        var routine=new DanceRoutine();
        if(routine.Update(true,0)!=DanceStage.Dress || routine.Update(true,800)!=DanceStage.Dancing
            || routine.Update(false,1200)!=DanceStage.Undress || routine.Update(false,1840)!=DanceStage.None)
            throw new InvalidOperationException("El disfraz de baile no completó sus transiciones.");
        var seen = new HashSet<int>();
        for(int i=0;i<5600;i++)
        {
            var p=Pose(i/100.0);
            seen.Add(p.Frame);
            if(!double.IsFinite(p.Angle) || Math.Abs(p.X)>12 || p.Y < -33 || p.Frame is <0 or >19)
                throw new InvalidOperationException("Una pose de baile salió del área reservada.");
        }
        if (seen.Count != 20) throw new InvalidOperationException("La rutina no utilizó todas las poses.");
    }
}
