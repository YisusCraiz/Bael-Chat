using System.IO;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

    private readonly ObservableCollection<AiProfile> profiles = new();
    private int editingIndex = -1;
    private bool loadingProfile;

    private readonly Dictionary<ChatMode, ModePersonality> personalities = new();
    private ChatMode? editingPersonality;
    private bool loadingPersonality;

    private readonly ObservableCollection<PdfEntry> documents = new();
    private readonly Dictionary<ChatMode, DocumentRule> documentRules = new();
    private readonly List<string> importedDocuments = new();
    private readonly HashSet<string> removedDocuments = new();
    private readonly CancellationTokenSource importLifetime = new();
    private ChatMode? editingDocumentMode;
    private bool documentsClosed;
    private bool importing;

    internal BaelSettings Settings { get; private set; }

    internal SettingsWindow(BaelSettings settings, bool unreadable)
    {
        InitializeComponent();
        Settings = settings;
        PersonalityTone.ItemsSource = ModePersonality.Tones;
        PersonalityDetail.ItemsSource = ModePersonality.Details;
        foreach (ChatMode mode in Enum.GetValues<ChatMode>())
        {
            personalities[mode] = settings.GetPersonality(mode) with { };
            PersonalityMode.Items.Add(Responses.Name(mode));
        }
        PersonalityMode.SelectedIndex = 0;
        PdfLibraryList.ItemsSource = documents;
        ModePdfList.ItemsSource = documents;
        foreach (PdfEntry document in settings.Documents)
            documents.Add(document);
        foreach (ChatMode mode in Enum.GetValues<ChatMode>())
        {
            documentRules[mode] = settings.GetDocumentRule(mode) with
            {
                DocumentIds = settings.GetDocumentRule(mode).DocumentIds.ToList()
            };
            DocumentMode.Items.Add(Responses.Name(mode));
        }
        DocumentMode.SelectedIndex = 0;
        PinWindow.IsChecked = settings.Topmost;
        AllowWalking.IsChecked = settings.Walk;
        DanceWithAudio.IsChecked = settings.DanceWithAudio;
        DanceThreshold.Value = Math.Clamp(settings.DanceThreshold * 100, 1, 30);
        IdleDelay.Value = Math.Clamp(settings.IdleSeconds, 10, 300);
        MessageThreshold.Value = Math.Clamp(settings.MessagesToUndress, 2, 10);
        Volume.Value = settings.Volume * 100;
        OnlineChat.IsChecked = settings.OnlineChat;
        AutoFallback.IsChecked = settings.AutoFallback;
        Provider.ItemsSource = AiProviders.All;
        foreach (AiProfile profile in settings.GetProfiles())
            profiles.Add(profile with { });
        ProfileList.ItemsSource = profiles;
        ProfileList.SelectedIndex = profiles.Count > 0 ? 0 : -1;
        ProfileEditor.IsEnabled = profiles.Count > 0;
        VoiceKey.Password = settings.VoiceKey;
        VoiceId.Text = settings.VoiceId;
        SpeechModel.Text = settings.SpeechModel;
        SpeakVoiceReplies.IsChecked = settings.SpeakVoiceReplies;
        Closed += (_, _) =>
        {
            StopMicTest();
            documentsClosed = true;
            importLifetime.Cancel();
            importLifetime.Dispose();
            if (DialogResult != true)
                foreach (string id in importedDocuments) PdfLibrary.Remove(id);
        };
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

    private void StoreDocumentRule()
    {
        if (editingDocumentMode is not ChatMode mode)
            return;
        documentRules[mode] = new DocumentRule
        {
            Limited = LimitToPdf.IsChecked == true,
            DocumentIds = ModePdfList.SelectedItems.Cast<PdfEntry>().Select(entry => entry.Id).ToList()
        };
    }

    private void ChangeDocumentMode(object sender, SelectionChangedEventArgs e)
    {
        StoreDocumentRule();
        if (DocumentMode.SelectedIndex < 0)
            return;
        editingDocumentMode = Enum.GetValues<ChatMode>()[DocumentMode.SelectedIndex];
        LoadDocumentRule();
    }

    private void LoadDocumentRule()
    {
        if (editingDocumentMode is not ChatMode mode)
            return;
        DocumentRule rule = documentRules[mode];
        LimitToPdf.IsChecked = rule.Limited;
        ModePdfList.SelectedItems.Clear();
        foreach (PdfEntry entry in documents.Where(entry => rule.DocumentIds.Contains(entry.Id)))
            ModePdfList.SelectedItems.Add(entry);
    }

    private async void AddPdf(object sender, RoutedEventArgs e)
    {
        if (importing) return;
        var picker = new OpenFileDialog
        {
            Title = "Añadir material de consulta",
            Filter = "Documentos PDF (*.pdf)|*.pdf",
            Multiselect = true
        };
        if (picker.ShowDialog(this) != true) return;

        importing = true;
        SaveSettingsButton.IsEnabled = false;
        AddPdfButton.IsEnabled = false;
        RemovePdfButton.IsEnabled = false;
        CancellationToken token = importLifetime.Token;
        var failures = new List<string>();
        int count = 0;
        try
        {
            foreach (string path in picker.FileNames)
            {
                PdfImportStatus.Text = $"Leyendo {Path.GetFileName(path)}…";
                try
                {
                    PdfEntry entry = await Task.Run(() => PdfLibrary.Import(path, token), token);
                    if (documentsClosed)
                    {
                        PdfLibrary.Remove(entry.Id);
                        return;
                    }
                    StoreDocumentRule();
                    importedDocuments.Add(entry.Id);
                    documents.Add(entry);
                    if (editingDocumentMode is ChatMode mode)
                    {
                        documentRules[mode].DocumentIds.Add(entry.Id);
                        LoadDocumentRule();
                    }
                    count++;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception error)
                {
                    string reason = error is InvalidDataException ? error.Message
                        : "No se pudo leer. Comprueba si tiene contraseña o está dañado.";
                    failures.Add($"{Path.GetFileName(path)}: {reason}");
                }
            }
            PdfImportStatus.Text = $"{count} PDF preparados. Guarda para conservarlos." +
                (failures.Count == 0 ? "" : "\n" + string.Join("\n", failures));
        }
        finally
        {
            importing = false;
            if (!documentsClosed)
            {
                SaveSettingsButton.IsEnabled = true;
                AddPdfButton.IsEnabled = true;
                RemovePdfButton.IsEnabled = true;
            }
        }
    }

    private void RemovePdf(object sender, RoutedEventArgs e)
    {
        if (PdfLibraryList.SelectedItem is not PdfEntry entry || importing)
            return;
        StoreDocumentRule();
        documents.Remove(entry);
        removedDocuments.Add(entry.Id);
        foreach (var rule in documentRules.Values)
            rule.DocumentIds.Remove(entry.Id);
        LoadDocumentRule();
        PdfImportStatus.Text = "Se quitará de la biblioteca al guardar. Tu archivo original se conserva.";
    }

    private void StorePersonality()
    {
        if (editingPersonality is not ChatMode mode)
            return;
        personalities[mode] = new ModePersonality
        {
            Tone = PersonalityTone.SelectedItem as string ?? ModePersonality.Default(mode).Tone,
            Humor = (int)HumorLevel.Value,
            Detail = PersonalityDetail.SelectedItem as string ?? "Equilibrado",
            Address = PersonalityAddress.Text.Trim(),
            Instructions = PersonalityInstructions.Text.Trim()
        };
    }

    private void ChangePersonalityMode(object sender, SelectionChangedEventArgs e)
    {
        if (loadingPersonality || PersonalityMode.SelectedIndex < 0)
            return;
        StorePersonality();
        editingPersonality = Enum.GetValues<ChatMode>()[PersonalityMode.SelectedIndex];
        LoadPersonality();
    }

    private void LoadPersonality()
    {
        if (editingPersonality is not ChatMode mode)
            return;
        loadingPersonality = true;
        ModePersonality personality = personalities[mode];
        PersonalityTone.SelectedItem = personality.Tone;
        HumorLevel.Value = Math.Clamp(personality.Humor, 0, 3);
        PersonalityDetail.SelectedItem = personality.Detail;
        PersonalityAddress.Text = personality.Address;
        PersonalityInstructions.Text = personality.Instructions;
        PersonalityPreview.Text = "Pulsa Ver ejemplo local para probar estos ajustes.";
        loadingPersonality = false;
    }

    private void PreviewPersonality(object sender, RoutedEventArgs e)
    {
        StorePersonality();
        if (editingPersonality is ChatMode mode)
            PersonalityPreview.Text = personalities[mode].FormatLocal(mode,
                Responses.For(mode, mode == ChatMode.English ? "presente" : "Quiero aprender"));
    }

    private void ResetPersonality(object sender, RoutedEventArgs e)
    {
        if (editingPersonality is ChatMode mode)
        {
            personalities[mode] = ModePersonality.Default(mode);
            LoadPersonality();
        }
    }

    private void StoreProfile()
    {
        if (editingIndex < 0 || editingIndex >= profiles.Count)
            return;
        bool wasLoading = loadingProfile;
        loadingProfile = true;
        profiles[editingIndex] = new AiProfile
        {
            Provider = Provider.SelectedValue as string ?? "Gemini",
            Model = ProfileModel.Text.Trim(),
            Key = ProfileKey.Password.Trim(),
            Enabled = ProfileEnabled.IsChecked == true
        };
        loadingProfile = wasLoading;
    }

    private void SelectProfile(object sender, SelectionChangedEventArgs e)
    {
        if (loadingProfile)
            return;
        int selected = ProfileList.SelectedIndex;
        StoreProfile();
        editingIndex = selected;
        LoadProfile();
    }

    private void LoadProfile()
    {
        loadingProfile = true;
        ProfileEditor.IsEnabled = editingIndex >= 0 && editingIndex < profiles.Count;
        if (ProfileEditor.IsEnabled)
        {
            AiProfile profile = profiles[editingIndex];
            Provider.SelectedValue = profile.Provider;
            ProfileModel.Text = profile.Model;
            ProfileKey.Password = profile.Key;
            ProfileEnabled.IsChecked = profile.Enabled;
            UpdateProviderHelp();
        }
        loadingProfile = false;
    }

    private void ChangeProvider(object sender, SelectionChangedEventArgs e)
    {
        if (loadingProfile || Provider.SelectedValue is not string name)
            return;
        ProfileModel.Text = AiProviders.Get(name).Model;
        ProfileKey.Password = "";
        UpdateProviderHelp();
    }

    private void UpdateProviderHelp()
    {
        if (Provider.SelectedValue is string name)
            ProviderHelp.Text = AiProviders.Get(name).Description;
    }

    private void AddProfile(object sender, RoutedEventArgs e)
    {
        StoreProfile();
        ProviderInfo choice = AiProviders.All.FirstOrDefault(info => profiles.All(profile => profile.Provider != info.Name))
            ?? AiProviders.All[0];
        profiles.Add(new AiProfile { Provider = choice.Name, Model = choice.Model });
        ProfileList.SelectedIndex = profiles.Count - 1;
    }

    private void RemoveProfile(object sender, RoutedEventArgs e)
    {
        int index = ProfileList.SelectedIndex;
        if (index < 0)
            return;
        loadingProfile = true;
        profiles.RemoveAt(index);
        editingIndex = -1;
        loadingProfile = false;
        ProfileList.SelectedIndex = Math.Min(index, profiles.Count - 1);
        editingIndex = ProfileList.SelectedIndex;
        LoadProfile();
    }

    private void MoveUp(object sender, RoutedEventArgs e) => MoveProfile(-1);
    private void MoveDown(object sender, RoutedEventArgs e) => MoveProfile(1);

    private void MoveProfile(int direction)
    {
        int index = ProfileList.SelectedIndex;
        int target = index + direction;
        if (index < 0 || target < 0 || target >= profiles.Count)
            return;
        StoreProfile();
        loadingProfile = true;
        profiles.Move(index, target);
        ProfileList.SelectedIndex = target;
        editingIndex = target;
        loadingProfile = false;
        LoadProfile();
    }

    private void OpenKeyPage(object sender, RoutedEventArgs e) => OpenProviderLink(false);
    private void OpenProviderDocs(object sender, RoutedEventArgs e) => OpenProviderLink(true);

    private void OpenProviderLink(bool docs)
    {
        if (Provider.SelectedValue is not string name)
            return;
        ProviderInfo info = AiProviders.Get(name);
        try
        {
            Process.Start(new ProcessStartInfo(docs ? info.DocsUrl : info.KeyUrl) { UseShellExecute = true });
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Status.Text = "Windows no pudo abrir el navegador.";
        }
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
        if (importing) return;
        StoreDocumentRule();
        StorePersonality();
        StoreProfile();
        foreach (AiProfile profile in profiles)
        {
            if (!AiProviders.All.Any(info => info.Name == profile.Provider)
                || !Regex.IsMatch(profile.Model, "^[a-zA-Z0-9][a-zA-Z0-9._/:-]*$")
                || profile.Key.Contains('\r') || profile.Key.Contains('\n'))
            {
                Status.Text = "Revisa el proveedor, el ID del modelo y la clave de cada conexión.";
                return;
            }
        }

        var updated = new BaelSettings
        {
            Topmost = PinWindow.IsChecked == true,
            IdleSeconds = (int)IdleDelay.Value,
            MessagesToUndress = (int)MessageThreshold.Value,
            Walk = AllowWalking.IsChecked == true,
            DanceWithAudio = DanceWithAudio.IsChecked == true,
            DanceThreshold = DanceThreshold.Value / 100,
            Volume = Volume.Value / 100,
            OnlineChat = OnlineChat.IsChecked == true,
            AutoFallback = AutoFallback.IsChecked == true,
            Profiles = profiles.ToList(),
            Personalities = new Dictionary<ChatMode, ModePersonality>(personalities),
            Documents = documents.ToList(),
            DocumentRules = new Dictionary<ChatMode, DocumentRule>(documentRules),
            VoiceKey = VoiceKey.Password.Trim(),
            VoiceId = VoiceId.Text.Trim(),
            SpeechModel = string.IsNullOrWhiteSpace(SpeechModel.Text) ? "scribe_v2" : SpeechModel.Text.Trim(),
            SpeakVoiceReplies = SpeakVoiceReplies.IsChecked == true,
            InputDeviceId = InputDevice.SelectedValue as string ?? Settings.InputDeviceId,
            OutputDeviceId = OutputDevice.SelectedValue as string ?? Settings.OutputDeviceId
        };

        if (updated.OnlineChat && !updated.GetProfiles().Any(profile => profile.Enabled && AiProviders.KeyFor(profile).Length > 0))
        {
            Status.Text = "Activa una conexión y agrega su clave para usar la IA en línea.";
            return;
        }

        try
        {
            updated.Save();
            foreach (string id in removedDocuments) PdfLibrary.Remove(id);
            Settings = updated;
            DialogResult = true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            Status.Text = "Windows no pudo guardar tus ajustes. Revisa el acceso a tu perfil.";
        }
    }
}
