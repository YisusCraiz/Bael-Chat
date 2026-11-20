using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Bael;

internal static class SettingsTheme
{
    internal static void Apply(Window window, bool dark)
    {
        string[] keys=["UiSurface","UiPanel","UiInset","UiInput","UiText","UiMuted","UiBorder","UiAccent","UiOnAccent","UiSelected","UiError","UiFocus"];
        string[] light=["#F3F2F8","#FFFFFF","#F1ECFA","#FBFAFE","#302847","#665D79","#D7CFE6","#7460C8","#FFFFFF","#E9E3F9","#9B2929","#503B91"];
        string[] night=["#17151F","#211E2C","#2B253B","#302A40","#F4F0FF","#BFB5D4","#51465F","#8B72DA","#FFFFFF","#44345F","#FFACAC","#C9B0FF"];
        for(int i=0;i<keys.Length;i++)
        {
            var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString((dark?night:light)[i]));
            brush.Freeze(); window.Resources[keys[i]]=brush;
        }
        window.Resources[SystemColors.WindowBrushKey]=window.Resources["UiInput"];
        window.Resources[SystemColors.WindowTextBrushKey]=window.Resources["UiText"];
        window.Resources[SystemColors.ControlBrushKey]=window.Resources["UiPanel"];
        window.Resources[SystemColors.ControlTextBrushKey]=window.Resources["UiText"];
        window.Resources[SystemColors.HighlightBrushKey]=window.Resources["UiSelected"];
        window.Resources[SystemColors.HighlightTextBrushKey]=window.Resources["UiText"];
        window.Resources[SystemColors.GrayTextBrushKey]=window.Resources["UiMuted"];
        TitleBar(window,dark);
    }
    internal static void TitleBar(Window window,bool dark)
    {
        nint handle=new WindowInteropHelper(window).Handle;
        if(handle==0) return;
        int enabled=dark?1:0;
        DwmSetWindowAttribute(handle,20,ref enabled,sizeof(int));
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
    internal static void Verify()
    {
        var window=new SettingsWindow(new BaelSettings { UiTheme="Oscuro" },false);
        try
        {
            var dark=(SolidColorBrush)window.FindResource("UiSurface");
            if(dark.Color.R>50 || ((SolidColorBrush)window.FindResource("UiText")).Color.R<220)
                throw new InvalidOperationException("El tema oscuro perdió su contraste.");
            Apply(window,false);
            if(((SolidColorBrush)window.FindResource("UiSurface")).Color.R<220)
                throw new InvalidOperationException("El tema claro no se restauró.");
        }
        finally {window.Close();}
    }
}
