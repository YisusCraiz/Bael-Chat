using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Bael;

public partial class SettingsWindow : Window
{
    private readonly DispatcherTimer meterTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private WasapiCapture? capture;
    private MMDevice? microphone;

    internal BaelSettings Settings { get; private set; }

    internal SettingsWindow(BaelSettings settings, bool unreadable)
    {
        InitializeComponent();
        Settings = settings;
        PinWindow.IsChecked = settings.Topmost;
        AllowWalking.IsChecked = settings.Walk;
        Volume.Value = settings.Volume * 100;
        OnlineChat.IsChecked = settings.OnlineChat;
        GeminiKey.Password = settings.GeminiKey;
        GeminiModel.Text = settings.GeminiModel;
        VoiceKey.Password = settings.VoiceKey;
        VoiceId.Text = settings.VoiceId;
        Closed += (_, _) => StopMicTest();
        meterTimer.Tick += (_, _) =>
        {
            if (microphone is null)
                return;
            try
            {
                MicLevel.Value = microphone.AudioMeterInformation.MasterPeakValue;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                StopMicTest();
                Status.Text = "El micrófono dejó de estar disponible.";
            }
        };

        if (unreadable)
            Status.Text = "No se pudo leer la configuración anterior. Guardar creará una nueva.";
    }

    internal void LoadAudioDevices()
    {
        try
        {
            InputDevice.ItemsSource = AudioDevices.List(DataFlow.Capture);
            OutputDevice.ItemsSource = AudioDevices.List(DataFlow.Render);
            InputDevice.SelectedValue = Settings.InputDeviceId;
            OutputDevice.SelectedValue = Settings.OutputDeviceId;
            if (InputDevice.SelectedIndex < 0)
                InputDevice.SelectedIndex = 0;
            if (OutputDevice.SelectedIndex < 0)
                OutputDevice.SelectedIndex = 0;

            if (!Equals(InputDevice.SelectedValue, Settings.InputDeviceId)
                || !Equals(OutputDevice.SelectedValue, Settings.OutputDeviceId))
                Status.Text = "Un dispositivo guardado está desconectado. Puedes seleccionar otro.";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            Status.Text = "Windows no pudo enumerar los dispositivos de audio.";
        }
    }

    private void ChangeInput(object sender, SelectionChangedEventArgs e) => StopMicTest();

    private void ToggleMicTest(object sender, RoutedEventArgs e)
    {
        if (capture is not null)
        {
            StopMicTest();
            return;
        }

        Status.Text = "";
        try
        {
            microphone = AudioDevices.Open(InputDevice.SelectedValue as string ?? "", DataFlow.Capture);
            var test = new WasapiCapture(microphone);
            capture = test;
            test.RecordingStopped += (_, args) => Dispatcher.InvokeAsync(() =>
            {
                if (capture != test)
                    return;

                StopMicTest();
                if (args.Exception is not null)
                    Status.Text = "Se interrumpió la prueba del micrófono.";
            });
            test.StartRecording();
            meterTimer.Start();
            MicTest.Content = "Detener prueba";
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException
                                      or UnauthorizedAccessException or InvalidOperationException)
        {
            StopMicTest();
            Status.Text = "No se pudo abrir el micrófono. Revisa la conexión y los permisos de Windows.";
        }
    }

    private void StopMicTest()
    {
        meterTimer.Stop();
        var previous = capture;
        capture = null;
        previous?.Dispose();
        microphone?.Dispose();
        microphone = null;
        if (MicLevel is not null)
            MicLevel.Value = 0;
        if (MicTest is not null)
            MicTest.Content = "Probar micrófono";
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        string model = GeminiModel.Text.Trim();
        if (model.StartsWith("models/", StringComparison.Ordinal))
            model = model["models/".Length..];

        if (model.Length > 0 && !Regex.IsMatch(model, "^[a-zA-Z0-9._-]+$"))
        {
            Status.Text = "Escribe un ID de modelo válido, por ejemplo gemini-2.5-flash.";
            return;
        }

        var updated = new BaelSettings
        {
            Topmost = PinWindow.IsChecked == true,
            Walk = AllowWalking.IsChecked == true,
            Volume = Volume.Value / 100,
            OnlineChat = OnlineChat.IsChecked == true,
            GeminiKey = GeminiKey.Password.Trim(),
            GeminiModel = model.Length == 0 ? new BaelSettings().GeminiModel : model,
            VoiceKey = VoiceKey.Password.Trim(),
            VoiceId = VoiceId.Text.Trim(),
            InputDeviceId = InputDevice.SelectedValue as string ?? Settings.InputDeviceId,
            OutputDeviceId = OutputDevice.SelectedValue as string ?? Settings.OutputDeviceId
        };

        if (updated.OnlineChat && updated.EffectiveGeminiKey.Length == 0)
        {
            Status.Text = "Agrega una clave de Gemini para activar el chat en línea.";
            return;
        }

        try
        {
            updated.Save();
            Settings = updated;
            DialogResult = true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            Status.Text = "Windows no pudo guardar tus ajustes. Revisa el acceso a tu perfil.";
        }
    }
}
