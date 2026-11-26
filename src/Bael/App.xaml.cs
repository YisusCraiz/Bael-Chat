using System.IO;
using System.Windows;

namespace Bael;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args is ["--system-inventory-test",var inventoryPath])
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try
            {
                var data=await SystemInventory.ReadAsync(CancellationToken.None);
                File.WriteAllText(inventoryPath,$"OK · CPU: {data.Cpu.Length} · módulos RAM: {data.Ram.Length} · discos: {data.Disks.Length} · notas: {data.Notes.Length}");
                Shutdown(0);
            }
            catch(Exception error) { File.WriteAllText(inventoryPath,error.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args is ["--smoke-test", var resultPath])
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DispatcherUnhandledException += (_,error) =>
            {
                File.AppendAllText(resultPath,"\nERROR UI: "+error.Exception.ToString());
                error.Handled=true; Shutdown(1);
            };
            File.WriteAllText(resultPath,"Iniciando pruebas de BAEL");
            try
            {
                var window = new MainWindow(MemoryJournal.Volatile());
                window.VerifyStartup();
                await window.VerifyStudentViewAsync();
                await window.VerifySpontaneousAsync();
                await window.VerifyCommandsAsync();
                await window.VerifyModeActivitiesAsync();
                File.AppendAllText(resultPath,"\nAbriendo Centro del sistema");
                window.VerifySystemPanelAccess();
                window.VerifyEnglishPanelAccess();
                File.AppendAllText(resultPath,"\nCentro del sistema verificado");
                await window.VerifyVoiceInterruptionAsync();
                await window.VerifyReplyCancellationAsync();
                window.Close();
                var companion = new DesktopWindow(testMode: true);
                companion.VerifyCompanion();
                await companion.VerifyBubbleVisibilityAsync();
                companion.Close();
                await ElevenLabsVoice.VerifyAsync();
                MemoryJournal.Verify();
                Bael.MainWindow.VerifyMemoryContinuation();
                await MemoryContextChecks.RunAsync();
                var noteData=new MemoryData();
                var memoryWindow=new MemoryWindow(noteData,new BaelSettings(),ChatMode.Free,()=>{},_=>{},()=>true,()=>true,()=>"Prueba");
                memoryWindow.VerifyInterface(); memoryWindow.Close();
                ConversationActivity.Verify();
                CompanionChatter.Verify();
                DanceRoutine.Verify();
                GlobalShortcuts.Verify();
                GlobalShortcuts.VerifyNative();
                GameSession.Verify();
                ModeActivities.Verify();
                var gameWindow = new GameWindow(ModePersonality.Default(ChatMode.Gamer));
                gameWindow.VerifyInterface();
                await gameWindow.VerifyCupsAsync();
                gameWindow.Close();
                AssistantCommands.Verify();
                SettingsTheme.Verify();
                SpeechInput.VerifyAudio();
                SpeechSilence.Verify();
                await SpeechInput.VerifyTranscriptionAsync();
                PdfLibrary.Verify();
                PdfQuizSession.Verify();
                await PdfStudyChecks.RunAsync();
                await ChatRouterChecks.RunAsync();
                await GeminiTransportChecks.RunAsync();
                File.AppendAllText(resultPath,"\nComprobando archivos, limpieza y lector del sistema");
                await SystemChecks.RunAsync(stage=>File.AppendAllText(resultPath,"\n"+stage));
                File.AppendAllText(resultPath,"\nComprobando el aula de inglés");
                await EnglishChecks.RunAsync();
                File.WriteAllText(resultPath, "OK");
                Shutdown(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(resultPath, error.ToString());
                Shutdown(1);
            }

            return;
        }

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow = new DesktopWindow();
        MainWindow.Show();
    }
}
