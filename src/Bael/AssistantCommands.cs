using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bael;

internal enum AssistantCommandKind { None, Help, AllModes, ChangeMode, ChooseMode }
internal readonly record struct AssistantCommand(AssistantCommandKind Kind, ChatMode? Target = null);

internal static class AssistantCommands
{
    private static string Normalize(string text)
    {
        string folded = new(text.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(Regex.Replace(folded.ToLowerInvariant(),"[^a-z0-9 ]"," ")," +"," ").Trim();
    }
    internal static AssistantCommand Parse(string message)
    {
        string text=Normalize(message);
        text=Regex.Replace(text,@"^(?:(?:oye|hola|hey) )?(?:(?:bael|bail|vael|vaira) )?","").Trim();
        text=Regex.Replace(text,@" (?:bael|bail|vael|vaira)$","").Trim();
        text=Regex.Replace(text,@"^por favor | por favor$","").Trim();
        text=Regex.Replace(text,@" (?:en este modo|en el modo actual|aqui)$","").Trim();
        if(Regex.IsMatch(text,@"^(?:no |nunca |como (?:digo|escribo)|traduce |explica la frase )")) return default;
        if(Regex.IsMatch(text,@"^(?:(?:dime|muestra|explicame) )?(?:(?:que|cuales) (?:son )?(?:tus |los )?modos(?: tienes| hay| disponibles)?|lista de modos|ver modos)$"))
            return new(AssistantCommandKind.AllModes);
        if(Regex.IsMatch(text,@"^(?:ayuda|tus funciones|que (?:(?:puedes|puedo|sabes) hacer|haces|funciones (?:tienes|tiene|hay)|hace este modo|opciones hay)|cuales son tus funciones|en que (?:me )?puedes ayudar(?:me)?|como (?:te uso|funcionas)|para que sirves|(?:dime|explicame|muestra|muestrame) (?:tus funciones|que puedes hacer|que funciones (?:tienes|tiene)))$"))
            return new(AssistantCommandKind.Help);
        Match change=Regex.Match(text,@"^(?:(?:(?:me )?(?:puedes|podrias) )|(?:quiero (?:que )?)|(?:me gustaria ))?(?:(?:cambia(?:me|te|r|rme|rte)?|cambies)|pasa(?:me|r|rme)?|pon(?:te|er|erme)?|activa(?:r)?|entra(?:r)?) (?:(?:a|al|en|el|un|de) )?(?:(?:el |otro |un )?modo(?: (?:a|al|de))? ?)?(?<target>.*)$");
        if(!change.Success) return default;
        string target=change.Groups["target"].Value.Trim();
        if (target.StartsWith("de ")) target=target[3..];
        if(target is "" or "modo" or "otro" or "otro modo" or "cualquier otro") return new(AssistantCommandKind.ChooseMode);
        foreach(var (alias,mode) in Aliases)
            if(target==alias || target.StartsWith(alias+" y ") || target==alias+" ahora") return new(AssistantCommandKind.ChangeMode,mode);
        return new(AssistantCommandKind.ChooseMode);
    }
    private static readonly (string,ChatMode)[] Aliases =
    [
        ("estudiante",ChatMode.Student),("estudio",ChatMode.Student),("estudiantil",ChatMode.Student),
        ("tarot",ChatMode.Tarot),("comedia",ChatMode.Comedy),("chistes",ChatMode.Comedy),
        ("sistema",ChatMode.System),("soporte",ChatMode.System),("tecnico",ChatMode.System),
        ("matematicas",ChatMode.Math),("matematica",ChatMode.Math),("algebra",ChatMode.Math),("math",ChatMode.Math),
        ("ingles",ChatMode.English),("english",ChatMode.English),("libre",ChatMode.Free),("normal",ChatMode.Free),
        ("charla",ChatMode.Free),("conversacion",ChatMode.Free),("gamer",ChatMode.Gamer),("juegos",ChatMode.Gamer),("apuestas",ChatMode.Gamer)
    ];
    internal static string AllModes() => "Mis modos son: " + string.Join(", ",Enum.GetValues<ChatMode>().Select(Responses.Name)) + ".\nPuedes decir: cambia al modo Inglés, quiero cambiar el modo a Sistema o ponte en modo Libre.";
    internal static string Help(ChatMode mode, BaelSettings settings)
    {
        string details=mode switch
        {
            ChatMode.Student=>"Te ayudo a organizar el estudio, repasar conceptos y consultar los PDF que asignes. Si está limitado a documentos, busco pasajes con archivo y página; no invento contenido que no esté en el material.",
            ChatMode.Math=>"Te acompaño con pistas de álgebra y fracciones, reviso tus intentos y tengo un reto guiado de tres pasos. Tú haces los cálculos: no te doy la solución completa.",
            ChatMode.English=>"Practicamos verbos en presente, pasado y futuro, preguntas, frases y pronunciación escrita. Tengo un quiz de cuatro ejercicios; con Leer en voz alta puedes escuchar frases. No califico tu pronunciación acústicamente.",
            ChatMode.System=>"Abre Centro del sistema para ver uso de CPU/RAM, espacio, discos, módulos RAM y componentes. Busca archivos por nombre o tamaño y analiza temporales antiguos; Limpiar selección elimina solo los elegidos tras verificarlos. Revisar hardware lee datos locales de Windows; algunos contadores pueden no estar disponibles. No diagnostico el estado eléctrico de la placa ni confirmo compatibilidad de ampliaciones. No modifico BIOS, controladores ni registro.",
            ChatMode.Tarot=>"Hago cartas y tiradas imaginarias para conversar y reflexionar. Puedes probar una carta del día o tres cartas; es entretenimiento, sin predecir el futuro.",
            ChatMode.Comedy=>"Tengo chistes del campus y puedo improvisar escenas o historias absurdas contigo. El tono y el humor se personalizan en Ajustes.",
            ChatMode.Free=>"Conversamos de tu día, gustos, anime o ideas; también hay una aventura espacial de decisiones. Puedo iniciar preguntas y curiosidades si activas Compañía. La conversación abierta usa una API; sin conexión hay actividades y repertorio local.",
            ChatMode.Gamer=>"Puedes abrir Vasos o Póker desde las actividades. Son partidas de espectáculo anunciadas donde BAEL siempre gana, con fichas ficticias, saldo inicial 1.000 y apuestas de 10, 50 o 100. En póker puedes cambiar hasta tres cartas.",
            _=>""
        };
        string activities=string.Join(" · ",(settings.GetDocumentRule(mode).Limited?PdfStudy.Activities(mode):ModeActivities.Catalog(mode)).Select(a=>a.Label));
        string controls=$"\nHablar: {Display(settings.TalkShortcut)}. Baile: {Display(settings.DanceShortcut)}. Agrandar/reducir: {Display(settings.EnlargeShortcut)} / {Display(settings.ShrinkShortcut)}.";
        controls += "\nEl botón Memoria permite consultar o borrar el historial y elegir recuerdos por modo. Sus opciones están en Ajustes → Memoria.";
        controls += "\nPulsa Hablar para interrumpir mi lectura y responder; Detener cancela la interacción actual. En el chat puedes desplegar los tiempos de cada etapa.";
        return $"Estoy en modo {Responses.Name(mode)}.\n{details}\nActividades: {activities}." +
            (settings.GetDocumentRule(mode).Limited ? "\nEste modo está limitado a sus PDF seleccionados. Tienes resumen extractivo, búsqueda por tema y repaso de palabras; la explicación con IA es opcional en Ajustes → Documentos." : "") +
            "\nPuedes pedirme cambiar de modo por voz o texto, por ejemplo: cambia al modo Gamer. Shift + clic derecho sobre el personaje abre modos y Ajustes."+controls;
    }
    private static string Display(string shortcut)=>string.IsNullOrWhiteSpace(shortcut)?"desactivado":shortcut;
    internal static void Verify()
    {
        foreach(var mode in Enum.GetValues<ChatMode>())
        {
            var command=Parse("BAEL, quiero cambiar el modo a "+Responses.Name(mode));
            if(command.Kind!=AssistantCommandKind.ChangeMode || command.Target!=mode
                || !Help(mode,new()).Contains(Responses.Name(mode))) throw new InvalidOperationException("Falló el cambio o la ayuda de un modo.");
        }
        foreach(string command in new[]{"¿Qué puedes hacer?","¿Qué funciones tienes?","En qué me puedes ayudar","ayuda"})
            if(Parse(command).Kind!=AssistantCommandKind.Help) throw new InvalidOperationException("No se reconoció una consulta de funciones.");
        if(Parse("puedes cambiarte al modo inglés por favor").Target!=ChatMode.English
            || Parse("quiero que cambies al modo sistema").Target!=ChatMode.System
            || Parse("ponte en modo normal").Target!=ChatMode.Free || Parse("cambia de modo").Kind!=AssistantCommandKind.ChooseMode
            || Parse("no cambies al modo gamer").Kind!=AssistantCommandKind.None
            || Parse("cómo digo cambia al modo inglés").Kind!=AssistantCommandKind.None
            || Parse("quiero resolver esta ecuación").Kind!=AssistantCommandKind.None)
            throw new InvalidOperationException("El comando confundió una orden con otra conversación.");
    }
}
