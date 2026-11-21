using System.IO;
using System.Diagnostics;
using System.Windows.Threading;
using System.Net.Http;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;

namespace Bael;

public partial class MainWindow : Window
{
    internal BaelSettings CurrentSettings => settings;
    internal event Action? UserSpoke;
    internal event Action<string>? SpeechNoticeChanged;
    internal bool IsCapturingVoice => speechRequest is not null;
    internal event Action<AssistantStage>? ActivityChanged;
    private AssistantStage speechStage;
    private bool waitingForReply;
    private AssistantStage displayedStage;
    private long activityStarted = Stopwatch.GetTimestamp();
    private readonly DispatcherTimer activityTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private ConversationTimings timings = new();
    private CancellationTokenSource? replyRequest;
    internal AssistantStage CurrentStage => speechStage != AssistantStage.Idle ? speechStage
        : waitingForReply ? AssistantStage.Thinking : voicePlayer is not null ? AssistantStage.Speaking
        : voiceRequest is not null ? AssistantStage.GeneratingVoice : AssistantStage.Idle;
    private void RefreshActivity()
    {
        if (isClosed) return;
        AssistantStage stage = CurrentStage;
        if (stage != displayedStage)
        { displayedStage = stage; activityStarted = Stopwatch.GetTimestamp(); ActivityChanged?.Invoke(stage); }
        ActivityStatus.Text = ConversationActivity.Label(stage) + (stage == AssistantStage.Idle ? "" : $" · {Stopwatch.GetElapsedTime(activityStarted).TotalSeconds:0.0} s");
        TimingStatus.Text = timings.Summary;
        StopInteractionButton.IsEnabled = stage != AssistantStage.Idle;
        if (stage == AssistantStage.Idle) activityTimer.Stop(); else if (!activityTimer.IsEnabled) activityTimer.Start();
    }
    private void StopInteraction(object sender, RoutedEventArgs e)
    {
        CancelSpontaneous(); speechRequest?.Cancel(); replyRequest?.Cancel(); StopVoice();
        SpeechNotice = "Interacción detenida. Puedes volver a hablar cuando aparezca Listo.";
    }
    private void CopyTimings(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(timings.Summary.Length == 0 ? "Todavía no hay tiempos medidos." : timings.Summary); }
        catch (System.Runtime.InteropServices.COMException) { ChatStatus.Text = "El portapapeles está ocupado."; }
    }
    private readonly CompanionChatter companionChatter = new();
    private CancellationTokenSource? spontaneousRequest;
    private string lastSpontaneous = "";
    private string spontaneousContext = "";
    private bool memoryOpen;
    internal bool CanStartSpontaneous => !isClosed && !memoryOpen && mode == ChatMode.Free && settings.SpontaneousChat
        && !settings.GetDocumentRule(ChatMode.Free).Limited && spontaneousRequest is null
        && speechRequest is null && voiceRequest is null && voicePlayer is null
        && CurrentStage == AssistantStage.Idle && !waitingForReply && SendButton.IsEnabled && string.IsNullOrWhiteSpace(Message.Text) && !activities.IsRunning(ChatMode.Free);
    private void CancelSpontaneous() => spontaneousRequest?.Cancel();
    internal void ResizeMascot(double scale)
    {
        settings = settings with { MascotScale = Math.Round(Math.Clamp(scale, 0.6, 1.8), 2) };
        try { settings.Save(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        { SpeechNotice = "El tamaño cambió para esta sesión, pero Windows no pudo guardar la preferencia."; }
        SettingsChanged?.Invoke(settings);
    }
    internal async Task StartSpontaneousAsync()
    {
        if (!CanStartSpontaneous) return;
        BaelSettings current = settings;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(chatLifetime.Token);
        spontaneousRequest = request;
        try
        {
            string reply = companionChatter.Local(current.GetPersonality(ChatMode.Free));
            if (current.OnlineChat)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(request.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                try
                {
                    ChatResult response = await router.ReplyAsync(current, ChatMode.Free,
                        CompanionChatter.Prompt(current.GetPersonality(ChatMode.Free),current.UseConversationContext?lastSpontaneous:""), [], timeout.Token, memoryContext:memory.ContextFor(ChatMode.Free));
                    if (!string.IsNullOrWhiteSpace(response.Text)) reply = response.Text;
                }
                catch (OperationCanceledException) when (!request.IsCancellationRequested) { }
            }
            if (request.IsCancellationRequested || isClosed || mode != ChatMode.Free || settings != current
                || !string.IsNullOrWhiteSpace(Message.Text) || !SendButton.IsEnabled || speechRequest is not null) return;
            if (reply.Length > 400) reply = reply[..397]+"…";
            lastSpontaneous = reply;
            spontaneousContext = reply;
            ShowReply(ChatMode.Free, reply);
            if (current.SpontaneousVoice) { timings = new ConversationTimings(); _ = ReadAloudAsync(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException or FormatException) { }
        finally { if (spontaneousRequest == request) spontaneousRequest = null; }
    }

    private string SpeechNotice
    {
        set { SpeechStatus.Text = value; SpeechNoticeChanged?.Invoke(value); }
    }
    internal void TalkFromShortcut() => Talk(this, new RoutedEventArgs());
    internal void SetDanceEnabled(bool enabled)
    {
        settings = settings with { DanceWithAudio = enabled };
        ApplySettings();
        try { settings.Save(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        { SpeechNotice = "El baile cambió para esta sesión, pero Windows no pudo guardar la preferencia."; }
        SettingsChanged?.Invoke(settings);
    }
    private void DragChat(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is TextBlock or Grid && e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    internal event Action<ChatMode, string>? ReplyReady;
    internal event Action<ChatMode>? ModeChanged;
    private ChatRouter router = new();
    private readonly ModeActivities activities = new();
    private GameWindow? games;
    private readonly Dictionary<ChatMode, List<ChatTurn>> histories;
    private readonly MemoryJournal memoryJournal;
    private readonly MemoryData memory;

    private void RenderConversation()
    {
        Conversation.Items.Clear();
        if(memory.Messages.TryGetValue(mode,out var lines))
            foreach(var line in lines) Conversation.Items.Add(new ConversationLine(line.IsUser?"Tú":$"BAEL · {Responses.Name(mode)}",line.Text,line.IsUser));
        latestReply = lines?.LastOrDefault(line=>!line.IsUser)?.Text;
        ReadAloudButton.IsEnabled = latestReply is not null;
        if(Conversation.Items.Count>0) Conversation.ScrollIntoView(Conversation.Items[Conversation.Items.Count-1]);
        UpdateMemoryStatus();
    }
    private void PersistMemory()
    { memoryJournal.Queue(memory.Snapshot(settings.SaveConversations)); UpdateMemoryStatus(); }
    private string MemoryStatusText()
    {
        if(memoryJournal.Error.Length>0) return memoryJournal.Error;
        if(memoryJournal.Busy) return "Guardando memoria local…";
        return settings.SaveConversations ? "Historial local protegido · últimos 100 mensajes por modo" : "Historial de esta sesión · guardado desactivado";
    }
    private void UpdateMemoryStatus() { if(!isClosed) MemoryStatus.Text = MemoryStatusText(); }
    private void AppendMemory(ChatMode profile,string text,bool user)
    {
        if(!memory.Messages.TryGetValue(profile,out var list)) { list=[]; memory.Messages[profile]=list; }
        list.Add(new(MemoryData.Clip(text,4000),user,DateTimeOffset.Now));
        if(list.Count>100) list.RemoveRange(0,list.Count-100);
        PersistMemory();
    }
    private void ManageMemory(object sender,RoutedEventArgs e) => OpenMemory(this);
    private void OpenMemory(Window owner)
    {
        CancelSpontaneous(); speechRequest?.Cancel(); replyRequest?.Cancel(); StopVoice();
        var dialog = new MemoryWindow(memory,settings,mode,PersistMemory,ForgetHistory,ResetMemory,
            ()=>!memoryJournal.Blocked,MemoryStatusText) { Owner=owner,Topmost=owner.Topmost };
        memoryOpen=true;
        try { dialog.ShowDialog(); }
        finally { memoryOpen=false; RenderConversation(); }
    }
    private void ForgetHistory(ChatMode selected)
    {
        CancelSpontaneous(); speechRequest?.Cancel(); replyRequest?.Cancel(); StopVoice();
        memory.Messages.Remove(selected); histories.Remove(selected); activities.Stop(selected);
        if(selected==ChatMode.Free) { lastSpontaneous=""; spontaneousContext=""; }
        PersistMemory(); if(selected==mode) RenderConversation();
    }
    private bool ResetMemory()
    {
        CancelSpontaneous(); speechRequest?.Cancel(); replyRequest?.Cancel(); StopVoice();
        if(!memoryJournal.Reset()) { UpdateMemoryStatus(); return false; }
        memory.Messages.Clear(); histories.Clear(); memory.Notes.Clear(); lastSpontaneous=""; spontaneousContext="";
        foreach(var selected in Enum.GetValues<ChatMode>()) activities.Stop(selected);
        RenderConversation(); return true;
    }


    internal void SelectMode(ChatMode selected)
    {
        CancelSpontaneous();
        if (mode != selected) { activities.Stop(mode); replyRequest?.Cancel(); StopVoice(); }
        mode = selected;
        ModeLabel.Text = $"Modo {Responses.Name(mode).ToLowerInvariant()}";
        UpdateActivities();
        RenderConversation();
        ModeChanged?.Invoke(mode);
        ChatStatus.Text = settings.GetDocumentRule(mode).Limited
            ? "Solo documentos · Consulta los PDF asignados en Ajustes."
            : mode == ChatMode.System
            ? "Escribe diagnóstico para una revisión local básica."
            : mode == ChatMode.Math ? "Escribe el ejercicio y tu intento. Te guío con pistas."
            : mode == ChatMode.English ? "Pide presente, pasado, futuro o pronunciación." : "";
    }
    internal event Action<BaelSettings>? SettingsChanged;

    internal void OpenConversation() { Chat.Visibility = Visibility.Visible; Message.Focus(); }

    internal void EditSettings(Window owner)
    {
        CancelSpontaneous(); speechRequest?.Cancel(); replyRequest?.Cancel(); StopVoice();
        var dialog = new SettingsWindow(settings, settingsUnreadable, OpenMemory) { Owner = owner, Topmost = owner.Topmost };
        dialog.LoadAudioDevices();
        if (dialog.ShowDialog() == true)
        {
            settings = dialog.Settings;
            settingsUnreadable = false;
            ApplySettings();
            if(!settings.UseConversationContext) { lastSpontaneous=""; spontaneousContext=""; }
            PersistMemory();
            SettingsChanged?.Invoke(settings);
        }
    }

    private BaelSettings settings;
    private bool settingsUnreadable;
    private readonly CancellationTokenSource chatLifetime = new();
    private WasapiOut? voicePlayer;
    private MMDevice? voiceOutput;
    private Mp3FileReader? voiceReader;
    private MemoryStream? voiceStream;
    private VolumeSampleProvider? voiceVolume;
    private CancellationTokenSource? voiceRequest;
    private string? latestReply;
    private bool isClosed;
    private CancellationTokenSource? speechRequest;
    private bool recording;
    private bool finishRecording;
    private ChatMode mode = ChatMode.Student;

    public MainWindow() : this(new MemoryJournal()) { }
    internal MainWindow(MemoryJournal journal)
    {
        memoryJournal=journal; memory=journal.Loaded.Snapshot(); histories=memory.Context;
        InitializeComponent();
        settings = BaelSettings.Load(out settingsUnreadable);
        memoryJournal.Completed += () =>
        {
            if(!Dispatcher.HasShutdownStarted) Dispatcher.InvokeAsync(()=> { if(!isClosed) UpdateMemoryStatus(); });
        };
        ApplySettings();
        if(!settings.SaveConversations) { memory.Messages.Clear(); histories.Clear(); memoryJournal.Queue(memory.Snapshot(false)); }
        SelectMode(ChatMode.Student);
        activityTimer.Tick += (_, _) => RefreshActivity();
        RefreshActivity();
        Message.TextChanged += (_, _) => CancelSpontaneous();
        Closed += (_, _) =>
        {
            CancelSpontaneous();
            games?.Close();
            isClosed = true;
            activityTimer.Stop();
            replyRequest?.Cancel();
            chatLifetime.Cancel();
            chatLifetime.Dispose();
            voiceRequest?.Cancel();
            speechRequest?.Cancel();
            ReleaseAudio();
            memoryJournal.Flush();
        };
    }

    internal void VerifyStartup()
    {
        var click = new RoutedEventArgs(Button.ClickEvent);
        if (!AllowsTransparency || WindowStyle != WindowStyle.None || ChatSurface.Background is not SolidColorBrush glass
            || glass.Color.A == 0)
            throw new InvalidOperationException("El chat no preparó su fondo translúcido.");
        OpenConversation();
        if (Chat.Visibility != Visibility.Visible)
            throw new InvalidOperationException("El chat no abrió.");

        foreach (ChatMode profile in Enum.GetValues<ChatMode>())
        {
            SelectMode(profile);
            if (ActivityButtons.Children.Count != 4) throw new InvalidOperationException("Faltan actividades en el chat.");
            const string message = "Mensaje de prueba";
            int previousCount = Conversation.Items.Count;
            AddUserMessage(message);
            ShowReply(profile, Responses.For(profile, message));
            if (Conversation.Items.Count != previousCount + 2 || string.IsNullOrWhiteSpace(latestReply))
                throw new InvalidOperationException($"El perfil {profile} no devolvió una respuesta.");
        }

        var settingsWindow = new SettingsWindow(settings, settingsUnreadable);
        settingsWindow.Close();

        int savedCount=Conversation.Items.Count;
        HideChat(this, click);
        OpenConversation();
        if (Chat.Visibility != Visibility.Visible || Conversation.Items.Count != savedCount)
            throw new InvalidOperationException("El chat perdió la conversación al volver a abrirlo.");
    }

    internal async Task VerifySpontaneousAsync()
    {
        BaelSettings previous = settings;
        try
        {
            settings = previous with { OnlineChat = false, SpontaneousChat = true, SpontaneousVoice = false,
                DocumentRules = new Dictionary<ChatMode, DocumentRule> { [ChatMode.Free] = new() { Limited = false } } };
            SelectMode(ChatMode.Free);
            int before = Conversation.Items.Count;
            if (!CanStartSpontaneous) throw new InvalidOperationException("La charla espontánea no quedó disponible.");
            await StartSpontaneousAsync();
            if (Conversation.Items.Count != before+1 || IsVisible || string.IsNullOrWhiteSpace(lastSpontaneous))
                throw new InvalidOperationException("El comentario abrió el chat o no quedó guardado.");
            Message.Text = "borrador";
            if (CanStartSpontaneous) throw new InvalidOperationException("Un comentario interrumpiría la escritura.");
            Message.Clear();
            settings = settings with { DocumentRules = new Dictionary<ChatMode, DocumentRule> { [ChatMode.Free] = new() { Limited = true } } };
            if (CanStartSpontaneous) throw new InvalidOperationException("Se ignoró el límite PDF al iniciar un comentario.");
        }
        finally { settings = previous; SelectMode(ChatMode.Student); }
    }

    internal async Task VerifyCommandsAsync()
    {
        BaelSettings previous = settings;
        try
        {
            settings = previous with { OnlineChat = true, Profiles = [],
                DocumentRules = Enum.GetValues<ChatMode>().ToDictionary(m => m,m => new DocumentRule { Limited = true }) };
            foreach (ChatMode current in Enum.GetValues<ChatMode>())
            {
                SelectMode(current); Message.Text = "¿Qué funciones tienes?";
                await SendCurrentMessage();
                if (latestReply is null || !latestReply.Contains("Actividades:"))
                    throw new InvalidOperationException("La ayuda no funcionó con un modo limitado a PDF.");
            }
            Message.Text = "Quiero cambiar el modo a inglés"; await SendCurrentMessage();
            if (mode != ChatMode.English || latestReply is null || !latestReply.Contains("activado") || IsVisible)
                throw new InvalidOperationException("El cambio local de modo falló o abrió el chat.");
        }
        finally { settings = previous; SelectMode(ChatMode.Student); }
    }

    internal async Task VerifyReplyCancellationAsync()
    {
        BaelSettings previous = settings; ChatRouter previousRouter = router;
        try
        {
            settings = previous with { OnlineChat = true,
                Profiles = [new AiProfile { Provider = "Groq", Model = "test", Key = "fake", Enabled = true }],
                DocumentRules = new Dictionary<ChatMode,DocumentRule> { [ChatMode.Free] = new() { Limited = false } } };
            foreach (bool changeMode in new[] { false,true })
            {
                var handler = new PendingReplyHandler();
                using var client = new HttpClient(handler); router = new ChatRouter(client);
                SelectMode(ChatMode.Free);
                int before = memory.Messages.TryGetValue(ChatMode.Free,out var priorLines)?priorLines.Count:0;
                string? previousReply = latestReply;
                Message.Text = "Pregunta de prueba";
                Task<bool> pending = SendCurrentMessage();
                if (CurrentStage != AssistantStage.Thinking || !handler.Requested)
                    throw new InvalidOperationException("No se inició la consulta cancelable.");
                if (changeMode) SelectMode(ChatMode.English); else StopInteraction(this,new RoutedEventArgs());
                handler.Result.SetResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Esta respuesta debe descartarse\"}}]}") });
                bool answered = await pending;
                if (answered || CurrentStage != AssistantStage.Idle || memory.Messages[ChatMode.Free].Count != before+1
                    || (!changeMode && latestReply != previousReply))
                    throw new InvalidOperationException("Una respuesta cancelada se publicó o dejó la app ocupada.");
            }
        }
        finally { router = previousRouter; settings = previous; SelectMode(ChatMode.Student); }
    }

    internal static void VerifyMemoryContinuation()
    {
        string folder=Path.Combine(Path.GetTempPath(),"BAEL-resume-"+Guid.NewGuid().ToString("N"));
        string file=Path.Combine(folder,"memory.dat");
        try
        {
            var first=new MainWindow(new MemoryJournal(file));
            first.settings=first.settings with { SaveConversations=true };
            first.SelectMode(ChatMode.Free); first.AddUserMessage("Retomar mi proyecto"); first.ShowReply(ChatMode.Free,"Seguimos con tu proyecto");
            first.histories[ChatMode.Free]=[new("user","Retomar mi proyecto"),new("model","Seguimos con tu proyecto")];
            first.memory.Notes.Add(new("chosen",ChatMode.Free,"Prefiero ejemplos cortos")); first.PersistMemory(); first.Close();
            var second=new MainWindow(new MemoryJournal(file));
            second.SelectMode(ChatMode.Free);
            if(second.Conversation.Items.Count!=2 || second.latestReply!="Seguimos con tu proyecto" || second.histories[ChatMode.Free].Count!=2)
                throw new InvalidOperationException("No se recuperó la conversación al volver a abrir.");
            second.SelectMode(ChatMode.English);
            if(second.Conversation.Items.Count!=0 || second.latestReply is not null) throw new InvalidOperationException("Se mezclaron historiales de modos.");
            second.SelectMode(ChatMode.Free); second.ForgetHistory(ChatMode.Free);
            if(second.Conversation.Items.Count!=0 || second.histories.ContainsKey(ChatMode.Free) || second.memory.Notes.Count!=1)
                throw new InvalidOperationException("El borrado del historial afectó los recuerdos elegidos.");
            second.Close();
            var after=new MemoryJournal(file);
            if(after.Loaded.Messages.ContainsKey(ChatMode.Free) || after.Loaded.Notes.Count!=1)
                throw new InvalidOperationException("El historial borrado reapareció.");
        }
        finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }

    private void UpdateActivities()
    {
        ActivityButtons.Children.Clear();
        ModeDescription.Text = ModeActivities.Description(mode);
        foreach (var activity in ModeActivities.Catalog(mode))
        {
            var button = new Button { Content = activity.Label, ToolTip = activity.Prompt,
                Padding = new Thickness(10, 6, 10, 6), FontSize = 12 };
            button.Click += async (_, _) =>
            {
                if (!SendButton.IsEnabled) return;
                if (activity.Prompt is "@cups" or "@poker")
                {
                    if (games is null)
                    {
                        games = new GameWindow(settings.GetPersonality(ChatMode.Gamer));
                        games.Owner = this;
                        games.Topmost = Topmost;
                        games.Closed += (_, _) => games = null;
                        games.Comment += text => ShowReply(ChatMode.Gamer, text);
                    }
                    games.SelectGame(activity.Prompt == "@poker");
                    games.Show();
                    games.Activate();
                    return;
                }
                Message.Text = activity.Prompt;
                await SendCurrentMessage();
            };
            ActivityButtons.Children.Add(button);
        }
    }

    private void HideChat(object sender, RoutedEventArgs e) => Hide();

    private void MessageKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            SendMessage(sender, e);
        }
    }

    private void ExplainFunctions(object sender, RoutedEventArgs e)
    { ShowReply(mode, AssistantCommands.Help(mode, settings)); }

    private bool HandleAssistantCommand(string message)
    {
        var command = AssistantCommands.Parse(message);
        switch (command.Kind)
        {
            case AssistantCommandKind.Help:
                ShowReply(mode, AssistantCommands.Help(mode, settings)); return true;
            case AssistantCommandKind.AllModes:
            case AssistantCommandKind.ChooseMode:
                ShowReply(mode, AssistantCommands.AllModes()); return true;
            case AssistantCommandKind.ChangeMode when command.Target is ChatMode selected:
                SelectMode(selected);
                ShowReply(selected, $"Modo {Responses.Name(selected)} activado. {ModeActivities.Description(selected)}\nPuedes preguntarme: ¿qué puedes hacer?");
                return true;
            default: return false;
        }
    }

    private void CopyReply(object sender, RoutedEventArgs e)
    {
        if (latestReply is null) return;
        try { Clipboard.SetText(latestReply); }
        catch (System.Runtime.InteropServices.COMException) { ChatStatus.Text = "El portapapeles está ocupado. Intenta otra vez."; }
    }

    private void OpenModes(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = Modes,
            Placement = PlacementMode.Bottom
        };

        foreach (ChatMode option in Enum.GetValues<ChatMode>())
        {
            var item = new MenuItem { Header = GetModeName(option), IsCheckable = true, IsChecked = option == mode };
            item.Click += (_, _) =>
            {
                SelectMode(option);
                Message.Focus();
            };
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    private void ApplySettings()
    {
        Topmost = settings.Topmost;
        ChatSurface.Background = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(255 * (1 - Math.Clamp(settings.ChatTransparency, 0, 0.65))), 245, 243, 250));
        if (voiceVolume is not null)
            voiceVolume.Volume = (float)Math.Clamp(settings.Volume, 0, 1);
    }

    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        EditSettings(this);
    }

    private async void SendMessage(object sender, RoutedEventArgs e) => await SendCurrentMessage();

    private async Task<bool> SendCurrentMessage(bool fromSpeech = false)
    {
        string message = Message.Text.Trim();
        if (message.Length == 0 || !SendButton.IsEnabled) return false;
        CancelSpontaneous();
        StopVoice();
        if (!fromSpeech) timings = new ConversationTimings();
        UserSpoke?.Invoke();
        ChatMode profile = mode;
        BaelSettings current = settings;
        AddUserMessage(message); Message.Clear(); Message.Focus(); ChatStatus.Text = "";
        if (HandleAssistantCommand(message)) { RefreshActivity(); return true; }
        bool limited = current.GetDocumentRule(profile).Limited;
        if (!limited && activities.TryReply(profile,message,out string activityReply))
        { ShowReply(profile,current.GetPersonality(profile).FormatLocal(profile,activityReply)); return true; }
        string reply = current.GetPersonality(profile).FormatLocal(profile,Responses.For(profile,message));
        if (!limited && profile == ChatMode.System && (message.Contains("diagn",StringComparison.OrdinalIgnoreCase)
            || message.Contains("examina",StringComparison.OrdinalIgnoreCase) || message.Contains("revisa",StringComparison.OrdinalIgnoreCase)))
        { ShowReply(profile,reply); return true; }
        if (!limited && !current.OnlineChat)
        { ChatStatus.Text = "Sin conexión · actividades y guías locales."; ShowReply(profile,reply); return true; }
        if (!histories.TryGetValue(profile,out var history)) { history = []; histories[profile] = history; }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(chatLifetime.Token);
        replyRequest = request; waitingForReply = true; SendButton.IsEnabled = false;
        ConversationTimings measured = timings;
        long started = Stopwatch.GetTimestamp();
        ChatStatus.Text = limited ? "Buscando en tus PDF…" : "Consultando las APIs activas…";
        RefreshActivity();
        try
        {
            if (limited)
            {
                reply = await Task.Run(() => PdfLibrary.Answer(current,profile,message),request.Token);
                request.Token.ThrowIfCancellationRequested();
                ChatStatus.Text = "Solo documentos · Pasajes del material seleccionado";
            }
            else
            {
                string routedMessage = current.UseConversationContext && profile == ChatMode.Free && spontaneousContext.Length > 0
                    ? "Contexto: BAEL inició antes este comentario: «" + spontaneousContext + "».\nMensaje actual del usuario: " + message : message;
                ChatResult result = await router.ReplyAsync(current,profile,routedMessage,history.ToArray(),request.Token,
                    new Progress<string>(status => { if (!isClosed && replyRequest == request && !request.IsCancellationRequested) ChatStatus.Text = status; }), memoryContext:memory.ContextFor(profile));
                if (!string.IsNullOrWhiteSpace(result.Text)) reply = result.Text;
                ChatStatus.Text = result.Status;
            }
            if (isClosed || request.IsCancellationRequested || mode != profile) return false;
            if (!limited)
            {
                if (profile == ChatMode.Free) spontaneousContext = "";
                history.Add(new ChatTurn("user",message)); history.Add(new ChatTurn("model",reply));
                if (history.Count > 12) history.RemoveRange(0,history.Count-12);
            }
            ShowReply(profile,reply); return true;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        { if (!isClosed) ChatStatus.Text = "Respuesta cancelada. Puedes enviar otra pregunta."; return false; }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or FormatException or JsonException or InvalidOperationException)
        {
            if (isClosed || request.IsCancellationRequested || mode != profile) return false;
            ChatStatus.Text = "No llegó la respuesta. Se mostró una guía local.";
            if (!limited) { ShowReply(profile,reply); return true; }
            return false;
        }
        finally
        {
            measured.Finish(limited ? "PDF" : "IA",started);
            if (replyRequest == request)
            { replyRequest = null; waitingForReply = false; if (!isClosed) SendButton.IsEnabled = true; }
            RefreshActivity();
        }
    }

    private void AddUserMessage(string message)
    {
        AppendMemory(mode,message,true);
        var line = new ConversationLine("Tú", message, true);
        Conversation.Items.Add(line);
        while(Conversation.Items.Count>100) Conversation.Items.RemoveAt(0);
        Conversation.ScrollIntoView(line);
    }

    private void ShowReply(ChatMode profile, string reply)
    {
        AppendMemory(profile,reply,false);
        if(profile==mode)
        {
            latestReply=reply; ReadAloudButton.IsEnabled=true;
            var line=new ConversationLine($"BAEL · {GetModeName(profile)}",reply,false);
            Conversation.Items.Add(line);
            while(Conversation.Items.Count>100) Conversation.Items.RemoveAt(0);
            Conversation.ScrollIntoView(line);
        }
        ReplyReady?.Invoke(profile,reply);
    }

    private async void Talk(object sender, RoutedEventArgs e) => await TalkAsync();

    private async Task TalkAsync(Func<CancellationToken,Task<byte[]?>>? recorder = null)
    {
        if(memoryOpen) { SpeechNotice="Cierra el gestor de Memoria antes de abrir el micrófono."; return; }
        CancelSpontaneous();
        if (speechRequest is not null)
        {
            if (recording) finishRecording = true;
            else { speechRequest.Cancel(); replyRequest?.Cancel(); StopVoice(); }
            return;
        }

        if (!SendButton.IsEnabled)
        {
            SpeechNotice = "Espera a que termine la respuesta actual.";
            return;
        }
        if (!string.IsNullOrWhiteSpace(Message.Text))
        {
            SpeechNotice = "Envía o borra el texto del cuadro antes de grabar.";
            return;
        }
        if (string.IsNullOrWhiteSpace(settings.EffectiveVoiceKey))
        {
            SpeechNotice = "Agrega la clave de ElevenLabs en Ajustes → Voz y audio.";
            return;
        }

        StopVoice();
        timings = new ConversationTimings();
        ConversationTimings measured = timings;
        long captureStarted = Stopwatch.GetTimestamp();
        long? readyAt = null;
        BaelSettings current = settings;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(chatLifetime.Token);
        speechRequest = request;
        finishRecording = false;
        recording = true;
        speechStage = AssistantStage.PreparingMicrophone;
        RefreshActivity();
        UserSpoke?.Invoke();
        SendButton.IsEnabled = false;
        Modes.IsEnabled = false;
        Message.IsReadOnly = true;
        ReadAloudButton.IsEnabled = false;
        TalkButton.Content = "Terminar frase";
        TalkLevel.Visibility = Visibility.Visible;
        SpeechNotice = "Preparando micrófono…";
        try
        {
            byte[]? audio = await (recorder is not null ? recorder(request.Token) : SpeechInput.RecordAsync(current, () => Volatile.Read(ref finishRecording),
                new Progress<float>(value => { if (!isClosed && speechRequest == request && recording) TalkLevel.Value = value; }),
                new Progress<string>(text =>
                {
                    if (!isClosed && speechRequest == request && recording)
                    {
                        if (readyAt is null)
                        { measured.Finish("Abrir micro",captureStarted); readyAt = Stopwatch.GetTimestamp(); }
                        speechStage = AssistantStage.Listening; SpeechNotice = text; RefreshActivity();
                    }
                }), request.Token));
            measured.Finish("Grabar",readyAt ?? captureStarted);
            recording = false;
            if (isClosed || request.IsCancellationRequested) return;
            TalkLevel.Visibility = Visibility.Collapsed;
            if (audio is null)
            {
                SpeechNotice = "No detecté voz. Revisa el micrófono seleccionado y sus permisos.";
                return;
            }

            TalkButton.Content = "Cancelar";
            speechStage = AssistantStage.Transcribing; RefreshActivity();
            long transcribingStarted = Stopwatch.GetTimestamp();
            SpeechNotice = "Transcribiendo con ElevenLabs…";
            string? text = await SpeechInput.TranscribeAsync(current, audio, request.Token);
            measured.Finish("Transcribir",transcribingStarted);
            if (isClosed || request.IsCancellationRequested) return;
            if (string.IsNullOrWhiteSpace(text))
            {
                SpeechNotice = "No se reconoció una frase. Pulsa Hablar e intenta otra vez.";
                return;
            }

            Message.Text = text;
            SpeechNotice = $"Te entendí: {text}";
            SendButton.IsEnabled = true;
            TalkButton.IsEnabled = false;
            speechStage = AssistantStage.Idle; RefreshActivity();
            bool answered = await SendCurrentMessage(fromSpeech:true);
            if (!isClosed && !request.IsCancellationRequested && answered && current.SpeakVoiceReplies && latestReply is not null)
                _ = ReadAloudAsync();
        }
        catch (OperationCanceledException)
        {
            if (!isClosed) SpeechNotice = request.IsCancellationRequested
                ? "Grabación o transcripción cancelada." : "La transcripción tardó demasiado. Intenta otra vez.";
        }
        catch (HttpRequestException error)
        {
            if (!isClosed) SpeechNotice = error.StatusCode is { } status
                ? $"ElevenLabs devolvió {(int)status}. Revisa el permiso Speech to Text, el modelo y la cuota de la clave."
                : "No se pudo conectar con ElevenLabs para transcribir.";
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException
                                      or InvalidOperationException or UnauthorizedAccessException
                                      or IOException or TimeoutException or JsonException or NAudio.MmException)
        {
            if (!isClosed) SpeechNotice = "No se pudo completar la entrada de voz. Revisa el micrófono, los permisos y el modelo de transcripción.";
        }
        finally
        {
            recording = false;
            if (speechRequest == request) { speechRequest = null; speechStage = AssistantStage.Idle; }
            RefreshActivity();
            if (!isClosed)
            {
                TalkButton.Content = "🎙 Hablar";
                TalkButton.IsEnabled = true;
                SendButton.IsEnabled = true;
                Modes.IsEnabled = true;
                Message.IsReadOnly = false;
                ReadAloudButton.IsEnabled = latestReply is not null;
                TalkLevel.Visibility = Visibility.Collapsed;
                TalkLevel.Value = 0;
            }
        }
    }

    private async void ReadAloud(object sender, RoutedEventArgs e) => await ReadAloudAsync();

    private bool OwnsVoice(CancellationTokenSource request) => !isClosed && voiceRequest == request && !request.IsCancellationRequested;
    private void StopVoice()
    {
        var previous = voiceRequest; voiceRequest = null; previous?.Cancel();
        EndPlayback(""); RefreshActivity();
    }
    private async Task ReadAloudAsync(Func<CancellationToken,Task<byte[]>>? generator = null)
    {
        if (voiceRequest is not null || voicePlayer is not null) { StopVoice(); return; }
        if (latestReply is not string text || isClosed) return;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(chatLifetime.Token);
        voiceRequest = request;
        ConversationTimings measured = timings;
        long started = Stopwatch.GetTimestamp();
        ReadAloudButton.Content = "Detener generación";
        VoiceStatus.Text = "Generando audio… Puedes pulsar Hablar para interrumpir.";
        RefreshActivity();
        try
        {
            byte[] audio = await (generator is null ? ElevenLabsVoice.GenerateAsync(settings,text,request.Token) : generator(request.Token));
            if (!OwnsVoice(request)) return;
            measured.Finish("Preparar voz",started);
            StartPlayback(audio);
        }
        catch (OperationCanceledException)
        { if (OwnsVoice(request)) EndPlayback("La generación de voz tardó demasiado. Intenta otra vez."); }
        catch (HttpRequestException error)
        { VoiceError(request,error.StatusCode is { } code ? $"ElevenLabs devolvió {(int)code}. Revisa la conexión de voz." : "No se pudo conectar con ElevenLabs."); }
        catch (InvalidOperationException error) { VoiceError(request,error.Message); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException
                                      or System.Runtime.InteropServices.COMException or NAudio.MmException)
        { VoiceError(request,"No se pudo reproducir el audio. Revisa el dispositivo de salida en Ajustes."); }
        finally
        {
            if (voiceRequest == request) voiceRequest = null;
            RefreshActivity();
        }
    }
    private void VoiceError(CancellationTokenSource request,string message)
    {
        if (!OwnsVoice(request)) return;
        EndPlayback(message); SpeechNoticeChanged?.Invoke("Voz: "+message);
    }
    private void EndPlayback(string status)
    {
        ReleaseAudio();
        if (isClosed) return;
        ReadAloudButton.Content = "Leer en voz alta";
        VoiceStatus.Text = status;
        RefreshActivity();
    }

    internal async Task VerifyVoiceInterruptionAsync()
    {
        BaelSettings previous = settings;
        settings = previous with { VoiceKey = "fake", SpeakVoiceReplies = false };
        try
        {
        var pending = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var recordingDone = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        latestReply = "Prueba de interrupción";
        Task pendingVoice = ReadAloudAsync(_ => pending.Task);
        var interrupted = voiceRequest!;
        Task listening = TalkAsync(_ => recordingDone.Task);
        if (!interrupted.IsCancellationRequested || voiceRequest is not null || CurrentStage != AssistantStage.PreparingMicrophone)
            throw new InvalidOperationException("Hablar no interrumpió la voz para abrir el micrófono.");
        pending.SetResult([0,1,2]); await pendingVoice;
        if (voicePlayer is not null || CurrentStage != AssistantStage.PreparingMicrophone)
            throw new InvalidOperationException("La voz tardía interrumpió la preparación del micrófono.");
        recordingDone.SetResult(null); await listening;
        var first = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task old = ReadAloudAsync(_ => first.Task);
        if (CurrentStage != AssistantStage.GeneratingVoice) throw new InvalidOperationException("No comenzó el estado de voz.");
        StopVoice();
        Task newer = ReadAloudAsync(_ => second.Task);
        var owner = voiceRequest;
        first.SetResult([0,1,2]); await old;
        if (voiceRequest != owner || CurrentStage != AssistantStage.GeneratingVoice)
            throw new InvalidOperationException("Una voz cancelada borró una solicitud posterior.");
        StopVoice(); speechStage = AssistantStage.Listening; RefreshActivity();
        second.SetResult([0,1,2]); await newer;
        if (voicePlayer is not null || CurrentStage != AssistantStage.Listening)
            throw new InvalidOperationException("Una respuesta tardía interrumpió la escucha.");
        speechStage = AssistantStage.Idle; RefreshActivity();
        }
        finally { settings = previous; speechStage = AssistantStage.Idle; StopVoice(); }
    }

    private void StartPlayback(byte[] audio)
    {
        voiceOutput = AudioDevices.Open(settings.OutputDeviceId, DataFlow.Render);
        voiceStream = new MemoryStream(audio, writable: false);
        voiceReader = new Mp3FileReader(voiceStream);
        voiceVolume = new VolumeSampleProvider(voiceReader.ToSampleProvider());
        var player = new WasapiOut(voiceOutput, AudioClientShareMode.Shared, true, 100);
        voicePlayer = player;
        player.PlaybackStopped += (_, args) => Dispatcher.InvokeAsync(() =>
        {
            if (voicePlayer == player)
                EndPlayback(args.Exception is null ? "" : "Se interrumpió la reproducción de voz.");
        });
        ApplySettings();
        player.Init(voiceVolume.ToWaveProvider());
        player.Play();
        ReadAloudButton.Content = "Detener audio";
        VoiceStatus.Text = "Reproduciendo… Pulsa Hablar o su atajo para interrumpirme.";
        RefreshActivity();
    }

    private void ReleaseAudio()
    {
        var player = voicePlayer;
        voicePlayer = null;
        if (player is not null)
        {
            try { player.Stop(); }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or NAudio.MmException or InvalidOperationException) { }
            try { player.Dispose(); }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or NAudio.MmException or InvalidOperationException) { }
        }
        voiceReader?.Dispose();
        voiceReader = null;
        voiceStream?.Dispose();
        voiceStream = null;
        voiceOutput?.Dispose();
        voiceOutput = null;
        voiceVolume = null;
    }

    private static string GetModeName(ChatMode option) => Responses.Name(option);
}

internal sealed record ConversationLine(string Speaker, string Text, bool IsUser)
{
    public string Background => IsUser ? "#66EBE4FA" : "#55F4F3F8";
    public Thickness Margin => IsUser ? new Thickness(38, 0, 0, 0) : new Thickness(0, 0, 38, 0);
}

internal sealed class PendingReplyHandler : HttpMessageHandler
{
    internal bool Requested { get; private set; }
    internal TaskCompletionSource<HttpResponseMessage> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    { Requested = true; return Result.Task; }
}
