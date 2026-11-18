using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Bael;

internal sealed class MascotSprites
{
    public ImageSource[] Idle { get; } = LoadFrames("idle");
    public ImageSource[] Walk { get; } = LoadFrames("walk");
    public ImageSource[] React { get; } = LoadFrames("react");

    private static ImageSource[] LoadFrames(string state)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Sprites", state);
        if (!Directory.Exists(directory))
            return [];

        try
        {
            var frames = new List<ImageSource>();
            foreach (string path in Directory.GetFiles(directory, "*.png").Order(StringComparer.Ordinal))
            {
                using var stream = File.OpenRead(path);
                var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                frame.Freeze();
                frames.Add(frame);
            }

            return frames.ToArray();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                     or FileFormatException or NotSupportedException)
        {
            return [];
        }
    }
}
