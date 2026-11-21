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
            try
            {
                var window = new MainWindow();
                window.VerifyStartup();
                await window.VerifySpontaneousAsync();
                await window.VerifyCommandsAsync();
                await window.VerifyVoiceInterruptionAsync();
                await window.VerifyReplyCancellationAsync();
                window.Close();
                var companion = new DesktopWindow(testMode: true);
                companion.VerifyCompanion();
                companion.Close();
                await ElevenLabsVoice.VerifyAsync();
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
                await ChatRouterChecks.RunAsync();
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
