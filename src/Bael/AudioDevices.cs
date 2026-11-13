using NAudio.CoreAudioApi;

namespace Bael;

internal sealed record AudioDevice(string Id, string Name);

internal static class AudioDevices
{
    public static AudioDevice[] List(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = new List<AudioDevice> { new("", "Predeterminado de Windows") };
        foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (device)
                devices.Add(new(device.ID, device.FriendlyName));
        }
        return devices.ToArray();
    }

    public static MMDevice Open(string id, DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        return id.Length == 0
            ? enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia)
            : enumerator.GetDevice(id);
    }
}
