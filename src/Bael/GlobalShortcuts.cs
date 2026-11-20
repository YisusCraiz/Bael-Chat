using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Bael;

internal readonly record struct Shortcut(uint Modifiers, uint VirtualKey)
{
    internal static bool TryParse(string value, out Shortcut shortcut)
    {
        shortcut = default;
        if (string.IsNullOrWhiteSpace(value)) return true;
        string[] parts = value.Split('+', StringSplitOptions.TrimEntries);
        uint modifiers = 0;
        foreach (string part in parts[..^1])
            switch (part.ToLowerInvariant())
            {
                case "ctrl": modifiers |= 2; break;
                case "alt": modifiers |= 1; break;
                case "shift": modifiers |= 4; break;
                case "win": modifiers |= 8; break;
                default: return false;
            }
        if (!Enum.TryParse<Key>(parts[^1], true, out var key) || key is Key.None or Key.System
            || key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return false;
        if ((modifiers & 11) == 0 && (key < Key.F1 || key > Key.F24)) return false;
        if (key == Key.F12) return false; // Reservada por Windows para depuración.
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return false;
        shortcut = new(modifiers, (uint)vk); return true;
    }
    internal static string Capture(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString()); return string.Join('+', parts);
    }
}

internal sealed class GlobalShortcuts : IDisposable
{
    private readonly nint handle;
    private readonly HwndSource source;
    private readonly Action talk, dance, enlarge, shrink;
    private readonly HashSet<int> registered = new();
    internal GlobalShortcuts(Window window, Action talk, Action dance, Action? enlarge = null, Action? shrink = null)
    {
        handle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(handle)!;
        this.talk = talk; this.dance = dance;
        this.enlarge = enlarge ?? (() => { }); this.shrink = shrink ?? (() => { });
        source.AddHook(Receive);
    }
    internal string Apply(BaelSettings settings)
    {
        Suspend();
        var failures = new List<string>();
        Register(9101, settings.TalkShortcut, "Hablar", failures);
        Register(9102, settings.DanceShortcut, "Baile", failures);
        Register(9103, settings.EnlargeShortcut, "Agrandar", failures);
        Register(9104, settings.ShrinkShortcut, "Reducir", failures);
        return string.Join("\n", failures);
    }
    private void Register(int id, string text, string label, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!Shortcut.TryParse(text, out var key) || !RegisterHotKey(handle,id,key.Modifiers | 0x4000,key.VirtualKey))
            failures.Add($"{label}: {text} no está disponible. Elige otra combinación en Ajustes → Pantalla y teclas.");
        else registered.Add(id);
    }
    private nint Receive(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != 0x0312 || !registered.Contains((int)wParam)) return 0;
        handled = true;
        switch ((int)wParam)
        { case 9101: talk(); break; case 9102: dance(); break; case 9103: enlarge(); break; case 9104: shrink(); break; }
        return 0;
    }
    internal void Suspend()
    {
        foreach (int id in registered) UnregisterHotKey(handle,id);
        registered.Clear();
    }
    public void Dispose() { Suspend(); source.RemoveHook(Receive); }
    [DllImport("user32.dll", SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    internal static void VerifyNative()
    {
        var one = new Window { ShowInTaskbar = false };
        var two = new Window { ShowInTaskbar = false };
        try
        {
            nint handle = new WindowInteropHelper(one).EnsureHandle();
            new WindowInteropHelper(two).EnsureHandle();
            int spoken = 0, danced = 0, enlarged = 0, shrunk = 0;
            using var first = new GlobalShortcuts(one, () => spoken++, () => danced++, () => enlarged++, () => shrunk++);
            using var second = new GlobalShortcuts(two, () => { }, () => { });
            var settings = new BaelSettings { TalkShortcut = "Ctrl+Alt+Shift+F10", DanceShortcut = "Ctrl+Alt+Shift+F11", EnlargeShortcut = "Ctrl+Alt+Shift+F8", ShrinkShortcut = "Ctrl+Alt+Shift+F9" };
            if (first.Apply(settings).Length > 0) throw new InvalidOperationException("No se registraron los atajos de prueba.");
            SendMessage(handle, 0x0312, 9101, 0); SendMessage(handle, 0x0312, 9102, 0);
            SendMessage(handle, 0x0312, 9103, 0); SendMessage(handle, 0x0312, 9104, 0);
            if (spoken != 1 || danced != 1 || enlarged != 1 || shrunk != 1 || second.Apply(settings).Length == 0)
                throw new InvalidOperationException("Falló el despacho o la detección de conflictos de atajos.");
            first.Suspend();
            if (second.Apply(settings).Length > 0) throw new InvalidOperationException("No se liberaron los atajos.");
            SendMessage(handle, 0x0312, 9101, 0);
            if (spoken != 1) throw new InvalidOperationException("Un atajo suspendido siguió activo.");
        }
        finally { one.Close(); two.Close(); }
    }

    internal static void Verify()
    {
        if (!Shortcut.TryParse("Ctrl+Alt+V",out var a) || !Shortcut.TryParse("Alt+Ctrl+V",out var b) || a!=b
            || Shortcut.TryParse("V",out _) || Shortcut.TryParse("Shift+A",out _) || Shortcut.TryParse("Ctrl+F12",out _)
            || !Shortcut.TryParse("",out _) || !Shortcut.TryParse("F8",out _) || !Shortcut.TryParse("Ctrl+Alt+Up",out _) || !Shortcut.TryParse("Ctrl+Alt+Down",out _))
            throw new InvalidOperationException("Validación de atajos incorrecta.");
        var expected = new BaelSettings { MascotScale = 1.4, EnlargeShortcut = "Ctrl+Shift+Up", ShrinkShortcut = "Ctrl+Shift+Down", SpontaneousSeconds = 90, SpontaneousVoice = true };
        var restored = System.Text.Json.JsonSerializer.Deserialize<BaelSettings>(System.Text.Json.JsonSerializer.Serialize(expected))!;
        if (restored.MascotScale != 1.4 || restored.EnlargeShortcut != expected.EnlargeShortcut || restored.ShrinkShortcut != expected.ShrinkShortcut || restored.SpontaneousSeconds != 90 || !restored.SpontaneousVoice)
            throw new InvalidOperationException("Se perdieron preferencias de tamaño o compañía.");
    }
}
