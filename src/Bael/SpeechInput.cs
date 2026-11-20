using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Bael;

internal static class SpeechInput
{
    public static async Task<byte[]?> RecordAsync(string deviceId, Func<bool> finish,
        IProgress<float> level, CancellationToken cancellationToken)
    {
        using var device = AudioDevices.Open(deviceId, DataFlow.Capture);
        using var capture = new WasapiCapture(device);
        using var buffer = new MemoryStream();
        using var writer = new WaveFileWriter(buffer, capture.WaveFormat);
        var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sync = new object();
        capture.DataAvailable += (_, args) =>
        {
            lock (sync) writer.Write(args.Buffer, 0, args.BytesRecorded);
        };
        capture.RecordingStopped += (_, args) => stopped.TrySetResult(args.Exception);

        var clock = Stopwatch.StartNew();
        var gate = new SpeechSilence();
        bool capturedVoice = false;
        capture.StartRecording();
        try
        {
            while (!finish() && clock.Elapsed < TimeSpan.FromSeconds(30))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (stopped.Task.IsCompleted)
                {
                    Exception? failure = await stopped.Task;
                    throw new InvalidOperationException("Se interrumpió el micrófono.", failure);
                }
                float peak = device.AudioMeterInformation.MasterPeakValue;
                level.Report(peak);
                if (peak >= 0.012f) capturedVoice = true;
                if (gate.ShouldFinish(peak, clock.ElapsedMilliseconds))
                    break;
                await Task.Delay(100, cancellationToken);
            }
        }
        finally
        {
            capture.StopRecording();
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        cancellationToken.ThrowIfCancellationRequested();
        Exception? error = await stopped.Task;
        if (error is not null)
            throw new InvalidOperationException("Se interrumpió el micrófono.", error);
        if (!capturedVoice) return null;
        lock (sync)
        {
            writer.Flush();
            return buffer.ToArray();
        }
    }

    public static async Task<string?> TranscribeAsync(BaelSettings settings, byte[] audio,
        CancellationToken cancellationToken, HttpClient? client = null)
    {
        using HttpRequestMessage request = BuildRequest(settings, audio);
        using HttpResponseMessage response = await (client ?? Client).SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        return json.RootElement.TryGetProperty("text", out var text) ? text.GetString()?.Trim() : null;
    }

    internal static async Task VerifyTranscriptionAsync()
    {
        using var client = new HttpClient(new TranscriptHandler());
        string? text = await TranscribeAsync(new BaelSettings
        {
            VoiceKey = "fake-key", SpeechModel = "scribe_v2"
        }, [82, 73, 70, 70], CancellationToken.None, client);
        if (text != "Quiero estudiar redes")
            throw new InvalidOperationException("La transcripción no devolvió el texto reconocido.");
    }

    private sealed class TranscriptHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string form = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (request.RequestUri!.AbsolutePath != "/v1/speech-to-text"
                || !request.Headers.Contains("xi-api-key") || !form.Contains("scribe_v2")
                || !form.Contains("bael-microphone.wav"))
                throw new InvalidOperationException("La entrada de voz no preparó la solicitud de transcripción.");
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"text\":\"Quiero estudiar redes\"}")
            };
        }
    }

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(45) };

    internal static HttpRequestMessage BuildRequest(BaelSettings settings, byte[] audio)
    {
        if (string.IsNullOrWhiteSpace(settings.EffectiveVoiceKey))
            throw new InvalidOperationException("Agrega la clave de ElevenLabs en Ajustes → Voz y audio.");
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.elevenlabs.io/v1/speech-to-text");
        request.Headers.Add("xi-api-key", settings.EffectiveVoiceKey);
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(settings.SpeechModel), "model_id");
        form.Add(new StringContent("false"), "tag_audio_events");
        var file = new ByteArrayContent(audio);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(file, "file", "bael-microphone.wav");
        request.Content = form;
        return request;
    }
}

internal sealed class SpeechSilence
{
    private long? lastVoice;

    public bool ShouldFinish(float level, long milliseconds)
    {
        if (level >= 0.012f) lastVoice = milliseconds;
        return lastVoice is long last && milliseconds - last >= 1600;
    }

    internal static void Verify()
    {
        var gate = new SpeechSilence();
        if (gate.ShouldFinish(0, 5000) || gate.ShouldFinish(0.1f, 5100) || gate.ShouldFinish(0, 6000)
            || !gate.ShouldFinish(0, 6800))
            throw new InvalidOperationException("La detección de fin de frase cortó la grabación.");
    }
}
