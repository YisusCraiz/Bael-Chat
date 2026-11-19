using NAudio.CoreAudioApi;

namespace Bael;

internal sealed class OutputAudioMonitor : IDisposable
{
    private MMDevice? device;
    private long nextRefresh;

    public float ReadLevel(long now)
    {
        try
        {
            if (now >= nextRefresh)
            {
                Dispose();
                nextRefresh = now + 2000;
                using var enumerator = new MMDeviceEnumerator();
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            return device?.AudioMeterInformation.MasterPeakValue ?? 0;
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException
                                      or InvalidOperationException)
        {
            Dispose();
            nextRefresh = now + 5000;
            return 0;
        }
    }

    public void Dispose()
    {
        device?.Dispose();
        device = null;
    }
}

internal sealed class DanceReaction
{
    private long activeUntil;

    public bool Update(bool enabled, float level, double threshold, long now)
    {
        if (!enabled)
        {
            activeUntil = 0;
            return false;
        }
        if (level >= threshold)
            activeUntil = now + 1100;
        return activeUntil > now;
    }

    internal static void Verify()
    {
        var reaction = new DanceReaction();
        if (reaction.Update(false, 1, 0.03, 1000))
            throw new InvalidOperationException("El baile desactivado reaccionó al audio.");
        if (reaction.Update(true, 0, 0.03, 1000))
            throw new InvalidOperationException("El silencio activó el baile.");
        if (!reaction.Update(true, 0.2f, 0.03, 1000)
            || !reaction.Update(true, 0, 0.03, 1500))
            throw new InvalidOperationException("El baile no mantuvo la reacción entre sonidos.");
        if (reaction.Update(true, 0, 0.03, 2200))
            throw new InvalidOperationException("El baile continuó después del silencio.");
    }
}
