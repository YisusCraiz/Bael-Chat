using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Bael;

internal sealed class EnglishAudio : IDisposable
{
    private object? sapi;
    private WasapiOut? player;
    internal async Task SpeakAsync(BaelSettings settings,string text,string engine,string level,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(engine=="ElevenLabs")
        {
            byte[] bytes=await ElevenLabsVoice.GenerateAsync(settings,text,token);
            token.ThrowIfCancellationRequested();
            using var device=AudioDevices.Open(settings.OutputDeviceId,DataFlow.Render);
            using var buffer=new MemoryStream(bytes,false); using var reader=new Mp3FileReader(buffer);
            using var output=new WasapiOut(device,AudioClientShareMode.Shared,true,100); player=output;
            var volume=new VolumeSampleProvider(reader.ToSampleProvider()) { Volume=(float)Math.Clamp(settings.Volume,0,1) };
            var ended=new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            output.PlaybackStopped+=(_,args)=>ended.TrySetResult(args.Exception);
            try
            {
                output.Init(volume.ToWaveProvider()); output.Play();
                Exception? error=await ended.Task.WaitAsync(TimeSpan.FromMinutes(2),token);
                if(error is not null) throw new InvalidOperationException("Se interrumpió el audio de la actividad.",error);
            }
            finally { output.Stop(); if(player==output) player=null; }
            return;
        }
        Type? type=Type.GetTypeFromProgID("SAPI.SpVoice");
        if(type is null) throw new InvalidOperationException("Windows no tiene síntesis de voz disponible. Instala una voz de inglés o selecciona ElevenLabs.");
        object instance=Activator.CreateInstance(type)!; sapi=instance;
        object? tokensObject=null;
        try
        {
            dynamic voice=instance; dynamic voices=voice.GetVoices(); tokensObject=voices;
            bool found=false;
            for(int i=0;i<(int)voices.Count;i++)
            {
                object candidate=voices.Item(i);
                try
                {
                    string languages=(string)((dynamic)candidate).GetAttribute("Language");
                    bool english=languages.Split(';').Any(code=>int.TryParse(code,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out int id) && (id&0x3ff)==9);
                    if(english) { voice.Voice=(dynamic)candidate; found=true; break; }
                }
                catch(COMException) { }
                finally { if(Marshal.IsComObject(candidate)) Marshal.FinalReleaseComObject(candidate); }
            }
            if(!found) throw new InvalidOperationException("No hay una voz de inglés instalada en Windows. Añádela en Configuración de Windows → Voz, o elige ElevenLabs en el aula.");
            voice.Rate=level=="A1"?-2:level=="A2"?-1:0; voice.Volume=(int)Math.Round(Math.Clamp(settings.Volume,0,1)*100);
            voice.Speak(text,1|2|16); // Async, purge queue, literal text (not XML or a filename).
            var watch=System.Diagnostics.Stopwatch.StartNew();
            while(!(bool)voice.WaitUntilDone(0))
            { token.ThrowIfCancellationRequested(); if(watch.Elapsed>TimeSpan.FromMinutes(2)) throw new TimeoutException(); await Task.Delay(80,token); }
        }
        finally
        {
            try { ((dynamic)instance).Speak("",1|2|16); } catch(COMException) { }
            if(tokensObject is not null && Marshal.IsComObject(tokensObject)) Marshal.FinalReleaseComObject(tokensObject);
            if(Marshal.IsComObject(instance)) Marshal.FinalReleaseComObject(instance);
            if(sapi==instance) sapi=null;
        }
    }
    internal void Stop()
    {
        player?.Stop();
        if(sapi is not null) try { ((dynamic)sapi).Speak("",1|2|16); } catch(COMException) { }
    }
    public void Dispose()=>Stop();
}
