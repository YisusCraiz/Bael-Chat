using System.Text.RegularExpressions;

namespace Bael;

internal sealed record EnglishQuestion(string Prompt,string[] Options,int Correct,string Why);
internal sealed record EnglishStep(string Prompt,string[] Keywords,string Hint);
internal sealed record EnglishLesson(string Id,string Level,string Title,string Goal,string Tip,string Writing,string Reading,string Listening,EnglishQuestion[] Questions,EnglishStep[] Steps)
{
    public string Label=>$"{Level} · {Title}";
}
internal static class EnglishCourse
{
    internal static readonly string[] Skills=["Escritura","Lectura","Escucha","Conversación"];
    internal static readonly string[] Levels=["A1","A2","B1","B2"];
    internal static readonly EnglishLesson[] Lessons=
    [
        new("a1-day","A1","Mi día","Presentarte y describir una rutina.","Usa I am para identidad y presente simple para hábitos. Con he/she, revisa la tercera persona.",
            "Write four sentences about your name, your morning, your studies, and something you like.",
            "My name is Alex. I live near my school. Every morning, I eat breakfast and walk to class. In the evening, I study English with a friend.",
            "Maya is a student. She lives with her brother near a small park. Every morning, she eats bread and drinks water before school. Classes start at eight. After school, Maya reads at home. On Fridays, she plays football with friends. She likes English because her teacher makes lessons very interesting.",
            [new("Who does Maya live with?",["Her brother","Her teacher","Her friends"],0,"El texto dice que vive con su hermano."),new("When do classes start?",["At seven","At eight","At nine"],1,"Classes start at eight."),new("What does she do on Fridays?",["She swims","She works","She plays football"],2,"Los viernes juega fútbol con amigos.")],
            [new("Hi! What is your name?",["name","am"],"My name is Alex. / I am Alex."),new("What do you do in the morning?",["eat","study","walk","work"],"In the morning, I eat breakfast."),new("What do you like about school?",["like","enjoy"],"I like learning English.")]),
        new("a1-cafe","A1","En la cafetería","Pedir comida con cortesía.","I would like expresa una petición cortés. Usa a/an con un sustantivo singular.",
            "Write a short order: a drink, some food, and a polite request for the price.",
            "I would like a cup of tea and a cheese sandwich, please. Can I have some water too? Thank you. How much is my order?",
            "Ben visits a cafe with his sister on Saturday. He orders a cheese sandwich and a glass of water. His sister chooses tea. The sandwich costs five dollars. They sit near the window and talk about school. Before leaving, Ben politely thanks the waiter and says he enjoyed the food.",
            [new("Who goes with Ben?",["His sister","His brother","His teacher"],0,"Visita la cafetería con su hermana."),new("What does Ben drink?",["Coffee","Water","Tea"],1,"Ben pide agua; su hermana elige té."),new("How much is the sandwich?",["Two dollars","Ten dollars","Five dollars"],2,"The sandwich costs five dollars.")],
            [new("Welcome! What would you like to drink?",["water","tea","coffee","juice"],"I would like some water, please."),new("And what would you like to eat?",["sandwich","cake","bread","salad"],"I would like a sandwich, please."),new("Anything else before I bring the bill?",["please","thank","much","bill"],"The bill, please. Thank you!")]),
        new("a2-weekend","A2","El fin de semana","Contar acciones pasadas y planes.","Usa pasado para lo terminado: went, visited, played. Después de did usa el verbo base.",
            "Write five sentences about last weekend and two plans for next weekend.",
            "Last Saturday, I visited my cousin. We cooked dinner and watched a film. The weather was rainy, so we stayed inside. Next weekend, we are going to visit a museum.",
            "Last weekend, Lena planned a picnic, but heavy rain changed her plans. Instead, she invited two friends to her flat. They cooked pasta and watched a comedy. Everyone brought something to share. Next Sunday, they hope to visit the park if the weather improves. Lena will bring some fresh fruit.",
            [new("Why did the plans change?",["Heavy rain","A late train","An exam"],0,"La lluvia fuerte cambió el picnic."),new("What did they cook?",["Rice","Pasta","Soup"],1,"They cooked pasta."),new("What will Lena bring next time?",["A film","Tea","Fresh fruit"],2,"Lena llevará fruta fresca el próximo domingo.")],
            [new("What did you do last weekend?",["went","visited","played","watched","stayed"],"I visited my cousin last weekend."),new("Why did you enjoy it?",["because","fun","interesting"],"I enjoyed it because it was fun."),new("What are you going to do next weekend?",["going","will","plan"],"I am going to visit a museum.")]),
        new("a2-travel","A2","Viaje y direcciones","Pedir información sobre horarios y rutas.","Usa Where/When/How para preguntar. There is/are describe lo que hay en un lugar.",
            "Write a message asking for directions to a station, the ticket price, and the departure time.",
            "Excuse me, where is the train station? Walk straight ahead and turn left at the bank. The station is beside the library. The next train leaves in twenty minutes.",
            "Omar arrived at the local station early because he wanted a quiet journey. His train leaves at ten, but the platform changed. A worker told him to use platform four, beside the cafe. Omar checked his ticket and bought water. He is travelling to visit his grandmother for the weekend.",
            [new("Why did Omar arrive early?",["For a quiet journey","To work","To buy a book"],0,"Quería un viaje tranquilo."),new("Which platform should he use?",["Two","Four","Ten"],1,"Un trabajador le indicó el andén cuatro."),new("Who will he visit?",["A colleague","His teacher","His grandmother"],2,"Viaja para visitar a su abuela.")],
            [new("Hello! Where would you like to go?",["station","museum","library","hotel"],"I would like to go to the station."),new("What information do you need about your ticket?",["much","price","cost","ticket"],"How much does the ticket cost?"),new("Is there anything else you need to know?",["when","time","leave","platform"],"When does the next train leave?")]),
        new("b1-work","B1","Resolver un problema","Explicar una dificultad y proponer soluciones.","Usa because para causas, could para opciones y should para recomendaciones; no va to después de should.",
            "Write an email explaining a small problem at work, two possible solutions, and the option you recommend.",
            "Our team missed a deadline because we received the information late. We could divide the remaining tasks and send a short update today. Next time, we should check the schedule together before starting.",
            "A small design team missed a deadline after receiving important information late. Rather than blaming one colleague, the manager asked everyone to suggest improvements. They decided to share progress every morning and identify delays earlier. The client accepted a revised schedule because the team explained the problem clearly and honestly.",
            [new("What caused the delay?",["Late information","A broken train","Missing paper"],0,"La información importante llegó tarde."),new("What change did the team agree on?",["Cancel meetings forever","Share progress every morning","Stop contacting clients"],1,"Acordaron compartir el progreso cada mañana."),new("Why did the client accept the new schedule?",["It was cheaper","They changed the design","They explained the problem honestly"],2,"La explicación clara y honesta ayudó al cliente a aceptar el nuevo calendario.")],
            [new("Tell me about a problem your team has faced.",["problem","late","missed","delay"],"We missed a deadline because the information arrived late."),new("What could you do differently?",["could","should","suggest"],"We could share progress every morning."),new("Which solution would you recommend, and why?",["because","recommend","therefore"],"I recommend daily updates because they help us find delays.")]),
        new("b1-study","B1","Aprender mejor","Explicar hábitos y comparar alternativas.","Usa comparativos: shorter than, more useful than. Explica motivos y ejemplos para apoyar tu opinión.",
            "Compare studying alone and studying with a partner. Give two reasons and describe your own plan.",
            "I used to study for several hours without a break. Now I work in shorter sessions and test myself afterwards. Studying with a partner helps me notice mistakes, although I still review difficult topics alone.",
            "Nora used to reread her notes for hours without checking what she remembered. Recently, she began studying in shorter sessions and answering practice questions afterwards. She also explains difficult topics to a partner. Although the new method feels challenging, Nora notices gaps earlier and spends less time reviewing familiar material.",
            [new("What was Nora's earlier habit?",["Rereading notes for hours","Explaining everything aloud","Taking practice tests first"],0,"Antes releía los apuntes durante horas."),new("What does she do after shorter sessions?",["Deletes her notes","Answers practice questions","Stops studying for a week"],1,"Después responde preguntas de práctica."),new("What benefit does she notice?",["No need to practise","Perfect memory","She identifies gaps earlier"],2,"Identifica antes lo que no comprende; no se afirma que tenga memoria perfecta.")],
            [new("How do you usually study?",["study","review","practice","read"],"I study in short sessions and review my notes."),new("How does that compare with studying in a group?",["than","more","less","although"],"Studying alone is quieter than studying in a group."),new("What would you change in your next study session?",["will","plan","could","going"],"I will answer practice questions after reading.")]),
        new("b2-environment","B2","Decisiones y entorno","Argumentar una postura con ventajas y límites.","Usa however, although y therefore para contrastar y conectar argumentos. Evita afirmaciones sin respaldo.",
            "Write a short argument about improving transport in a town. Include a benefit, a limitation, and a balanced recommendation.",
            "Although cycling can reduce traffic, new lanes should be planned with local residents. A route that looks efficient on a map may still be inconvenient for people with limited mobility. Public discussion helps reveal those tradeoffs.",
            "A town proposed replacing parking spaces with cycle lanes. Supporters argued that safer routes would encourage cycling, while shopkeepers worried about deliveries. After consulting residents, planners kept loading areas and improved bus connections. The final plan addressed several concerns, although its long term effects still require careful monitoring and evaluation.",
            [new("What concerned shopkeepers?",["Deliveries","School uniforms","Library hours"],0,"Les preocupaban las entregas al quitar estacionamiento."),new("What did planners retain?",["All parking spaces","Loading areas","Only bicycle access"],1,"Mantuvieron zonas de carga y mejoraron la conexión de autobuses."),new("What remains uncertain?",["Whether a consultation happened","Whether lanes were proposed","The long term effects"],2,"Los efectos a largo plazo requieren seguimiento; el texto no garantiza el éxito.")],
            [new("Should a town replace parking spaces with cycle lanes? Explain your position.",["because","although","support","oppose"],"I support safer cycle lanes because they may reduce traffic."),new("What is the strongest objection to your view?",["however","concern","although","delivery","deliveries"],"However, businesses may need reliable delivery access."),new("Suggest a compromise and explain how you would evaluate it.",["could","monitor","evaluate","measure"],"We could keep loading areas and monitor the effects.")]),
        new("b2-tech","B2","Tecnología y criterio","Evaluar una propuesta considerando evidencia y límites.","Distingue posibilidad de certeza: may, might, could. Apoya una recomendación con condiciones verificables.",
            "Discuss whether an organisation should use automated tools for important decisions. Include one benefit, one risk, and conditions for responsible use.",
            "An automated recommendation may sound confident without being well supported. Before relying on it, users should check the evidence and consider what the system cannot observe. Clear explanations help, but they do not remove the need for judgement.",
            "A company tested an automated tool to organise customer requests. It reduced repetitive work, but sometimes misclassified unusual cases. Staff therefore reviewed uncertain results before taking action. The trial suggested useful benefits, yet managers refused to claim complete reliability. They planned further testing with different requests before expanding the system.",
            [new("What benefit appeared in the trial?",["Less repetitive work","No unusual cases","No need for staff"],0,"El beneficio observado fue reducir trabajo repetitivo."),new("How were uncertain results handled?",["Deleted automatically","Reviewed by staff","Sent without checking"],1,"El personal los revisaba antes de actuar."),new("What did managers decide?",["Claim perfect reliability","Remove every safeguard","Test more before expanding"],2,"Decidieron probar con otras solicitudes antes de ampliar el uso.")],
            [new("What benefit could an automated tool offer your organisation?",["could","may","reduce","help"],"It could reduce repetitive work and help staff focus on difficult cases."),new("What limitation would you warn users about?",["however","risk","might","uncertain"],"However, it might misclassify unusual requests."),new("Under what conditions would you recommend using it?",["if","provided","review","test","monitor"],"I would recommend it if staff review uncertain results and monitor errors.")])
    ];
    internal static string LevelDescription(string level)=>level switch
    { "A1"=>"A1 · Primeros pasos: frases cortas sobre ti y tu día.","A2"=>"A2 · Situaciones cotidianas: cuenta experiencias y pide información.","B1"=>"B1 · Conecta tus ideas: explica problemas, razones y planes.",_=>"B2 · Argumenta con claridad: compara ideas y explica sus límites." };
    internal static string WritingSpanish(string id)=>id switch
    {
        "a1-day"=>"Escribe cuatro frases: tu nombre, qué haces por la mañana, qué estudias y algo que te gusta.",
        "a1-cafe"=>"Imagina que estás en una cafetería. Pide una bebida y algo de comer, y pregunta el precio con cortesía.",
        "a2-weekend"=>"Cuenta en cinco frases qué hiciste el fin de semana pasado. Añade dos planes para el próximo.",
        "a2-travel"=>"Escribe un mensaje para preguntar cómo llegar a la estación, cuánto cuesta el boleto y a qué hora sale el tren.",
        "b1-work"=>"Redacta un correo sobre un problema pequeño de trabajo. Propón dos soluciones y explica cuál recomiendas.",
        "b1-study"=>"Compara estudiar a solas y con un compañero. Da dos razones y explica qué harás para estudiar mejor.",
        "b2-environment"=>"Propón cómo mejorar el transporte de una ciudad. Explica una ventaja, una limitación y una recomendación equilibrada.",
        _=>"Explica si una organización debería usar herramientas automáticas para tomar decisiones importantes. Incluye un beneficio, un riesgo y condiciones de uso responsable."
    };

    internal static int WordCount(string text)=>Regex.Matches(text,@"\b[A-Za-z]+(?:['’][A-Za-z]+)?\b").Count;
    internal static EnglishLesson Get(string id)=>Lessons.FirstOrDefault(l=>l.Id==id)??Lessons[0];
}
