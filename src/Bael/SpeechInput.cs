using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Bael;

internal static class SpeechInput
{
    public static Task<byte[]?> RecordAsync(BaelSettings settings, Func<bool> finish,
        IProgress<float> level, IProgress<string> status, CancellationToken cancellationToken) =>
        Task.Run(() => RecordCoreAsync(settings, finish, level, status, cancellationToken), cancellationToken);

    private static async Task<byte[]?> RecordCoreAsync(BaelSettings settings, Func<bool> finish,
        IProgress<float> level, IProgress<string> status, CancellationToken token)
    {
        using var device = AudioDevices.Open(settings.InputDeviceId, DataFlow.Capture);
        using var capture = new WasapiCapture(device, true, 30);
        using var buffer = new MemoryStream();
        var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sync = new object();
        float peak = 0;
        long firstVoice = -1, lastVoice = 0;
        double threshold = Math.Clamp(settings.SpeechThreshold, 0.001, 0.05);
        capture.DataAvailable += (_, args) =>
        {
            float measured = SamplePeak(args.Buffer, args.BytesRecorded, capture.WaveFormat);
            Volatile.Write(ref peak, measured);
            lock (sync)
            {
                if (measured >= threshold)
                {
                    if (firstVoice < 0) firstVoice = buffer.Length;
                    lastVoice = buffer.Length + args.BytesRecorded;
                }
                buffer.Write(args.Buffer, 0, args.BytesRecorded);
            }
        };
        capture.RecordingStopped += (_, args) => stopped.TrySetResult(args.Exception);
        token.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();
        var gate = new SpeechSilence(threshold, settings.SpeechPauseMilliseconds);
        capture.StartRecording();
        status.Report("Te escucho… Habla ahora; otra pulsación termina la frase.");
        try
        {
            while (!finish() && clock.Elapsed < TimeSpan.FromSeconds(30))
            {
                token.ThrowIfCancellationRequested();
                if (stopped.Task.IsCompleted) throw new InvalidOperationException("Se interrumpió el micrófono.", await stopped.Task);
                float measured = Volatile.Read(ref peak);
                level.Report(measured);
                if (gate.ShouldFinish(measured, clock.ElapsedMilliseconds) || gate.NoVoiceTimeout(clock.ElapsedMilliseconds)) break;
                await Task.Delay(30, token).ConfigureAwait(false);
            }
        }
        finally
        {
            capture.StopRecording();
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        Exception? error = await stopped.Task;
        if (error is not null) throw new InvalidOperationException("Se interrumpió el micrófono.", error);
        byte[] raw;
        lock (sync)
        {
            if (firstVoice < 0) return null;
            int align = capture.WaveFormat.BlockAlign;
            long pad = capture.WaveFormat.AverageBytesPerSecond / 4;
            int start = (int)Math.Max(0, firstVoice - pad); start -= start % align;
            int end = (int)Math.Min(buffer.Length, lastVoice + pad); end -= end % align;
            raw = buffer.GetBuffer().AsSpan(start, end-start).ToArray();
        }
        return CompactWave(raw, capture.WaveFormat);
    }

    internal static float SamplePeak(byte[] bytes, int count, WaveFormat format)
    {
        bool floating = format.Encoding == WaveFormatEncoding.IeeeFloat || format is WaveFormatExtensible ext
            && ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
        int step = format.BitsPerSample / 8;
        if (step is < 1 or > 4) return 0;
        float peak = 0;
        for (int i=0;i+step<=count;i+=step)
        {
            float sample;
            if (floating && step == 4) sample = BitConverter.ToSingle(bytes,i);
            else sample = step switch
            {
                1 => (bytes[i]-128)/128f,
                2 => BitConverter.ToInt16(bytes,i)/32768f,
                3 => ((bytes[i] | bytes[i+1]<<8 | bytes[i+2]<<16) << 8 >> 8)/8388608f,
                4 => BitConverter.ToInt32(bytes,i)/2147483648f,
                _ => 0
            };
            if (float.IsFinite(sample)) peak = Math.Max(peak,Math.Abs(sample));
        }
        return Math.Min(peak,1);
    }

    internal static byte[] CompactWave(byte[] raw, WaveFormat format)
    {
        if (format is WaveFormatExtensible ext)
            format = ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71")
                ? WaveFormat.CreateIeeeFloatWaveFormat(ext.SampleRate,ext.Channels)
                : new WaveFormat(ext.SampleRate,ext.BitsPerSample,ext.Channels);
        using var wrapped = new MemoryStream();
        using (var writer = new WaveFileWriter(wrapped,format)) writer.Write(raw,0,raw.Length);
        using var input = new MemoryStream(wrapped.ToArray());
        using var reader = new WaveFileReader(input);
        ISampleProvider source = new MonoSamples(reader.ToSampleProvider());
        if (source.WaveFormat.SampleRate != 16000) source = new WdlResamplingSampleProvider(source,16000);
        using var output = new MemoryStream();
        using (var writer = new WaveFileWriter(output,new WaveFormat(16000,16,1)))
        {
            float[] samples = new float[1600];
            int read;
            while ((read=source.Read(samples,0,samples.Length))>0) writer.WriteSamples(samples,0,read);
        }
        return output.ToArray();
    }

    private sealed class MonoSamples(ISampleProvider source) : ISampleProvider
    {
        private float[] input = [];
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate,1);
        public int Read(float[] buffer, int offset, int count)
        {
            int channels = source.WaveFormat.Channels;
            if(input.Length < count*channels) input = new float[count*channels];
            int read=source.Read(input,0,count*channels), frames=read/channels;
            for(int f=0;f<frames;f++)
            { float sum=0; for(int c=0;c<channels;c++) sum+=input[f*channels+c]; buffer[offset+f]=sum/channels; }
            return frames;
        }
    }

    internal static void VerifyAudio()
    {
        var format=WaveFormat.CreateIeeeFloatWaveFormat(48000,2);
        byte[] raw=new byte[48000*2*4];
        for(int i=0;i<48000;i++)
        {
            float sample=(float)(0.1*Math.Sin(2*Math.PI*440*i/48000));
            BitConverter.GetBytes(sample).CopyTo(raw,i*8); BitConverter.GetBytes(sample).CopyTo(raw,i*8+4);
        }
        if(SamplePeak(raw,raw.Length,format)<0.09f) throw new InvalidOperationException("No se detectaron muestras de voz.");
        byte[] compact=CompactWave(raw,format);
        using var reader=new WaveFileReader(new MemoryStream(compact));
        if(reader.WaveFormat.SampleRate!=16000 || reader.WaveFormat.Channels!=1 || reader.WaveFormat.BitsPerSample!=16
            || compact.Length>34000 || compact.Length<30000 || Math.Abs(reader.TotalTime.TotalSeconds-1)>0.05)
            throw new InvalidOperationException("El audio compacto perdió duración, formato o tamaño.");
        if(SamplePeak(BitConverter.GetBytes((short)-16384),2,new WaveFormat(16000,16,1))<0.49f)
            throw new InvalidOperationException("No se midió correctamente audio PCM.");
    }

    public static async Task<string?> TranscribeAsync(BaelSettings settings, byte[] audio,
        CancellationToken cancellationToken, HttpClient? client = null, string? language = null)
    {
        using HttpRequestMessage request = BuildRequest(settings, audio, language);
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

    internal static HttpRequestMessage BuildRequest(BaelSettings settings, byte[] audio, string? language = null)
    {
        if (string.IsNullOrWhiteSpace(settings.EffectiveVoiceKey))
            throw new InvalidOperationException("Agrega la clave de ElevenLabs en Ajustes → Voz y audio.");
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.elevenlabs.io/v1/speech-to-text");
        request.Headers.Add("xi-api-key", settings.EffectiveVoiceKey);
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(settings.SpeechModel), "model_id");
        form.Add(new StringContent("false"), "tag_audio_events");
        if(language is not null) form.Add(new StringContent(language),"language_code");
        var file = new ByteArrayContent(audio);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(file, "file", "bael-microphone.wav");
        request.Content = form;
        return request;
    }
}

internal sealed class SpeechSilence(double threshold = 0.004, int pauseMilliseconds = 900)
{
    private long? lastVoice;
    public bool ShouldFinish(float level, long milliseconds)
    {
        if (level >= Math.Clamp(threshold,0.001,0.05)) lastVoice = milliseconds;
        return lastVoice is long last && milliseconds - last >= Math.Clamp(pauseMilliseconds,600,2200);
    }
    public bool NoVoiceTimeout(long milliseconds) => lastVoice is null && milliseconds >= 8000;
    internal static void Verify()
    {
        var gate = new SpeechSilence();
        if (gate.ShouldFinish(0, 5000) || gate.ShouldFinish(0.005f, 5100) || gate.ShouldFinish(0, 5900)
            || !gate.ShouldFinish(0, 6000) || gate.NoVoiceTimeout(9000))
            throw new InvalidOperationException("La detección de fin de frase perdió voz suave o su pausa.");
        var silent=new SpeechSilence();
        if(silent.NoVoiceTimeout(7999) || !silent.NoVoiceTimeout(8000)) throw new InvalidOperationException("El silencio esperó demasiado.");
        var slow=new SpeechSilence(0.004,1800); slow.ShouldFinish(0.05f,100);
        if(slow.ShouldFinish(0,1000) || !slow.ShouldFinish(0,1900)) throw new InvalidOperationException("La pausa configurable no se aplicó.");
    }
}
