namespace Bael;

internal sealed class ChatterClock
{
    private long nextDue;
    internal void Reset(long now, int seconds, bool welcome = false) =>
        nextDue = now + (welcome ? Math.Min(20, Math.Clamp(seconds,30,900)) : Math.Clamp(seconds,30,900))*1000L;
    internal bool Poll(long now, bool allowed, int seconds)
    {
        if(nextDue == 0) Reset(now,seconds);
        if(!allowed) { nextDue = Math.Max(nextDue,now+5000); return false; }
        if(now < nextDue) return false;
        Reset(now,seconds); return true;
    }
    internal static void Verify()
    {
        var clock=new ChatterClock(); clock.Reset(1000,180,true);
        if(clock.Poll(20000,true,180) || !clock.Poll(21000,true,180) || clock.Poll(21001,true,180))
            throw new InvalidOperationException("Los comentarios se repitieron sin esperar.");
        clock.Reset(1000,30);
        if(clock.Poll(31000,false,30) || clock.Poll(32000,true,30) || !clock.Poll(36000,true,30))
            throw new InvalidOperationException("Un comentario interrumpió una interacción.");
    }
}

internal sealed class CompanionChatter
{
    private int previous=-1;
    private static readonly string[] Facts =
    [
        "El 2 es el único número primo par: cualquier otro número par es divisible entre 2. ¿Te gustan estos pequeños trucos matemáticos?",
        "Un grupo de 8 bits tiene 256 combinaciones posibles. Ocho interruptores pequeñitos dan bastante juego, ¿verdad?",
        "En inglés, go cambia a went en pasado. ¿Qué verbo irregular siempre se te escapa?",
        "«Oso» es un palíndromo: se lee igual de izquierda a derecha que al revés. ¿Se te ocurre otro?",
        "El sonido necesita un medio para propagarse; en el vacío no viaja como lo hace por el aire. ¿Habías pensado en eso al ver una película espacial?",
        "En un triángulo de geometría euclidiana, los ángulos interiores suman 180 grados. ¿Quieres inventar uno y comprobarlo?",
        "Un kilómetro equivale a mil metros. A veces convertir una unidad grande en pequeñas piezas hace más fácil entender un problema.",
        "El número cero es par: puede escribirse como 2 multiplicado por un entero, en este caso el propio cero."
    ];
    private static readonly string[] Questions =
    [
        "Si hoy pudieras aprender una habilidad al instante, ¿cuál elegirías?",
        "¿Qué fue lo más curioso que te pasó hoy? También acepto pequeñas victorias.",
        "Tengo ganas de una historia. ¿Nos mandamos al espacio o abrimos una cafetería para unicornios?",
        "¿Qué canción pondrías de fondo para tu día de hoy?",
        "Si inventaras un videojuego, ¿cuál sería su primera misión?",
        "¿Qué personaje de una serie invitarías a pasar una tarde y por qué?",
        "¿Hay algún proyecto que tengas ganas de empezar, aunque sea en versión pequeñita?",
        "Pausa de compañero: ¿prefieres una curiosidad, una pregunta rara o una historia improvisada?"
    ];
    private static readonly string[] Quips =
    [
        "He revisado mi agenda y tengo reservado un rato para hacerme el interesante. ¿Te sumas o me dejas hablando con el cuerno?",
        "Si mi vida fuera un anime, este sería el episodio de descanso. Aunque tengo sprites suficientes para una escena de baile.",
        "Estoy practicando mi entrada dramática. Me falta viento en el cabello y alguien que anuncie mi nombre.",
        "Mi traje de unicornio no mejora el Wi-Fi, pero sí mi confianza al bailar. Cada quien presume sus habilidades.",
        "Pregunta de máxima importancia académica: ¿un unicornio necesita gorra o viene con antena de serie?",
        "Me dieron ganas de inventar un nombre para nuestra próxima aventura. Yo propongo Operación Cuerno Cósmico. ¿Tú qué pondrías?"
    ];
    internal string Local(ModePersonality personality)
    {
        var lines=Facts.Concat(Questions).Concat(personality.Humor>0 ? Quips : []).ToArray();
        int index=Random.Shared.Next(lines.Length);
        if(index==previous) index=(index+1)%lines.Length;
        previous=index;
        string prefix=personality.Tone switch
        {
            "Formal y respetuoso" => "Permíteme compartir algo. ",
            "Misterioso y teatral" => "Algo cruza el pequeño escenario de hoy… ",
            "Paciente y motivador" => "Una pequeña pausa puede traer una buena idea. ",
            _ => ""
        };
        string address=string.IsNullOrWhiteSpace(personality.Address)?"":personality.Address+", ";
        return address+prefix+lines[index];
    }
    internal static string Prompt(ModePersonality personality, string last) =>
        "Inicia tú un comentario espontáneo de compañero, sin que el usuario haya preguntado. " +
        "Aplica el carácter, humor y preferencias de expresión configurados. Elige entre una pregunta natural, una curiosidad estable que conozcas con certeza o una ocurrencia acorde al humor. " +
        "No inventes datos, recuerdos ni actividad del usuario, ni sugieras que puedes ver lo que hace en su PC. Sin noticias ni datos actuales. " +
        "Máximo 2 frases, 45 palabras, una sola pregunta como máximo. Si el humor es 0 evita bromas y no hagas de comediante. " +
        "No repitas este comentario anterior: «"+last+"».";
    internal static void Verify()
    {
        var chatter=new CompanionChatter();
        string previous="";
        for(int i=0;i<100;i++)
        {
            string line=chatter.Local(new ModePersonality {Humor=0,Address="Yisus"});
            if(line==previous || !line.StartsWith("Yisus, ")) throw new InvalidOperationException("Los comentarios perdieron variedad o trato.");
            previous=line;
        }
        ChatterClock.Verify();
    }
}
