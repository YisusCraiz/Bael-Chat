using System.IO;
using System.Windows;

namespace Bael;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args is ["--smoke-test", var resultPath])
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DispatcherUnhandledException += (_,error) =>
            {
                File.WriteAllText(resultPath,error.Exception.ToString());
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
                File.WriteAllText(resultPath,"Abriendo Centro del sistema");
                window.VerifySystemPanelAccess();
                File.WriteAllText(resultPath,"Centro del sistema verificado");
                await window.VerifyVoiceInterruptionAsync();
                await window.VerifyReplyCancellationAsync();
                window.Close();
                var companion = new DesktopWindow(testMode: true);
                companion.VerifyCompanion();
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
                File.WriteAllText(resultPath,"Comprobando archivos, limpieza y lector del sistema");
                await SystemChecks.RunAsync();
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
