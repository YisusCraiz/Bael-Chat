using System.Diagnostics;

namespace Bael;

internal enum AssistantStage { Idle, PreparingMicrophone, Listening, Transcribing, Thinking, GeneratingVoice, Speaking }
internal readonly record struct ConversationPose(int Frame, double X, double Y, double Angle, double ScaleY);
internal static class ConversationActivity
{
    internal static string Label(AssistantStage stage) => stage switch
    {
        AssistantStage.PreparingMicrophone => "Abriendo micrófono",
        AssistantStage.Listening => "Te escucho",
        AssistantStage.Transcribing => "Entendiendo tu frase",
        AssistantStage.Thinking => "Pensando la respuesta",
        AssistantStage.GeneratingVoice => "Preparando mi voz",
        AssistantStage.Speaking => "Hablando · puedes interrumpirme",
        _ => "Listo para conversar"
    };
    internal static ConversationPose Pose(AssistantStage stage, double seconds)
    {
        double t=Math.Max(0,seconds), wave=Math.Sin(t*3);
        return stage switch
        {
            AssistantStage.PreparingMicrophone => new(1,0,-1,wave*2,1),
            AssistantStage.Listening => new((int)(t*0.6)%4==3?1:0,2,-1,5+wave*1.2,1),
            AssistantStage.Transcribing or AssistantStage.Thinking => new((int)t%3==2?1:0,0,-Math.Abs(wave)*2,-4+wave*2,1),
            AssistantStage.GeneratingVoice => new(1,0,-Math.Abs(wave)*2,wave*3,1),
            AssistantStage.Speaking => new(2+(int)(t*4)%2,Math.Sin(t*2)*2,-Math.Abs(Math.Sin(t*6))*2,Math.Sin(t*3)*2,1+Math.Sin(t*6)*0.012),
            _ => new(0,0,0,0,1)
        };
    }
    internal static void Verify()
    {
        foreach(var stage in Enum.GetValues<AssistantStage>())
            for(int i=0;i<100;i++)
            {
                var pose=Pose(stage,i/10.0);
                if(pose.Frame is <0 or >3 || !double.IsFinite(pose.Angle) || Math.Abs(pose.Angle)>7 || string.IsNullOrWhiteSpace(Label(stage)))
                    throw new InvalidOperationException("Un gesto de conversación es inválido.");
            }
        var timings=new ConversationTimings(); timings.Record("Transcribir",1200); timings.Record("IA",350);
        if(!timings.Summary.Contains("Transcribir") || !timings.Summary.Contains("IA") || timings.Summary.Contains("Grabar"))
            throw new InvalidOperationException("El indicador mezcló etapas que no se midieron.");
    }
}

internal sealed class ConversationTimings
{
    private readonly Dictionary<string,double> values=new();
    private static readonly string[] Order=["Abrir micro","Grabar","Transcribir","PDF","IA de PDF","IA","Preparar voz"];
    internal void Record(string step,double milliseconds) => values[step]=Math.Max(0,milliseconds);
    internal void Finish(string step,long start) => Record(step,Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    internal string Summary => string.Join(" · ",Order.Where(values.ContainsKey).Select(step=>$"{step}: {values[step]/1000:0.00} s"));
}
