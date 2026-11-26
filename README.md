# BAEL-UMAN

Aplicación de escritorio para Windows desarrollada en C# y WPF.

## Instalar BAEL

[Descargar el instalador BAEL-Setup.exe](https://github.com/YisusCraiz/Bael-Chat/releases/latest/download/BAEL-Setup.exe)

Ejecuta el instalador en Windows x64. Instala la aplicación para tu usuario, incluye .NET y los sprites y crea un acceso en el menú Inicio. Puedes elegir un acceso en el escritorio. Abre BAEL desde Inicio y configura tus claves en Ajustes. Se puede desinstalar desde Aplicaciones instaladas de Windows.

Cada publicación en main genera una versión descargable cuando pasan la compilación y las pruebas de instalación, arranque y desinstalación. El instalador inicial no tiene firma digital.

## Abrir en Visual Studio

1. En Windows, abre **Visual Studio Installer** y elige **Modificar** en Visual Studio 2022 (17.8 o posterior).
2. Marca **Desarrollo de escritorio de .NET**. Comprueba que tienes el **SDK de .NET 8** con `dotnet --list-sdks`. Si no aparece una versión 8.0, instala el [SDK de .NET 8](https://dotnet.microsoft.com/es-es/download/dotnet/8.0).
3. Clona el repositorio con GitHub Desktop y selecciona `feature/base-wpf` mientras el PR inicial esté abierto. Cuando se integre, podrás usar `main`.
4. Abre `Bael.sln` en Visual Studio y pulsa **F5**. Visual Studio restaura automáticamente NAudio y el paquete de protección de datos de Windows al compilar.

## Descargar y ejecutar desde GitHub

1. Inicia sesión en GitHub y abre [Compilar BAEL en Windows](https://github.com/YisusCraiz/Bael-Chat/actions/workflows/windows-build.yml).
2. Abre la compilación exitosa más reciente y descarga **BAEL-Windows-x64** desde **Artifacts**.
3. Extrae todo el ZIP y abre `BAEL-UMAN.exe` en Windows x64. El paquete incluye el runtime de .NET.

## Mascota sobre el escritorio

BAEL inicia como chico chibi con fondo transparente. Arrástralo para colocarlo dentro del escritorio principal. Pulsa **Shift + clic derecho** sobre el personaje para abrir la rueda de burbujas. Cada modo tiene una miniatura y un nombre; el engrane abre Ajustes. Pulsa fuera de la rueda o Escape para cerrarla. También puedes abrirla con Shift + F10 cuando BAEL tiene el foco. El engrane está encima del sprite y abre una ventana flotante. Un clic en el personaje abre el chat; cerrar el chat lo oculta y conserva la conversación durante la sesión. Para salir usa la burbuja × de la rueda.

En el modo Estudiante, tras 30 segundos sin interacción, el chico se viste de unicornio con una transformación mágica y camina por el escritorio. Abrir el chat descubre su cabeza. Cuatro mensajes enviados en un minuto retiran el resto del disfraz. El chat abierto, el cursor sobre la mascota o los ajustes abiertos pausan el tiempo de inactividad. Estos tiempos se ajustan desde General. Hablar corresponde a mensajes escritos en el chat.

## Modos

Estudiante es el modo inicial. Su disfraz de unicornio y sus transformaciones pertenecen a ese modo. Tarot, Comedia, Sistema, Matemáticas e Inglés tienen cuatro sprites propios: reposo, parpadeo y dos gestos de actividad. Los gestos aparecen al cambiar de modo o enviar un mensaje. También puedes cambiar de modo desde el chat.

- **Estudiante:** organización y explicaciones de estudio. Material de estudio asignable desde Documentos.
- **Tarot:** tirada ficticia y reflexión.
- **Comedia:** humor del campus.
- **Sistema:** preguntas para orientar problemas sencillos. Escribe diagnóstico para leer Windows, procesadores y espacio en discos locales. La información permanece local; los mensajes que envíes a Gemini siguen la configuración del chat.
- **Matemáticas:** pistas y preguntas para que el alumno resuelva su ejercicio y muestre su siguiente paso. La instrucción de Gemini pide evitar dar el resultado completo.
- **Inglés:** presente, pasado, futuro y aproximaciones de pronunciación. El botón de voz permite escuchar la respuesta con ElevenLabs.

Sin Gemini los modos usan guías locales básicas. Con Gemini activado se conserva hasta seis intercambios por modo durante la sesión para continuar los ejercicios. La prueba de micrófono sigue siendo un medidor local; la práctica de pronunciación ofrece texto y audio de ejemplo.

Los PNG incluidos están organizados en `src/Bael/Sprites/desktop`: `boy` (polo gris), `headless` (cabeza descubierta), `unicorn` (disfraz completo) y `magic` (transformación), cuatro fotogramas por grupo. Se copian automáticamente al compilar y publicar.

## Comprobación en GitHub

Antes de publicar el ZIP, GitHub Actions inicializa la ventana de BAEL en Windows, ejecuta los manejadores del chat con Tarot y Comedia y comprueba que la conversación siga presente al ocultar y reabrir el panel. También comprueba la transparencia y carga de las nueve secuencias de sprites y la respuesta del vestuario a la conversación. Para la revisión visual, prueba las animaciones, el arrastre y la voz en tu PC.

## Trabajar en equipo

Crea una rama desde `main` actualizado y abre un PR por tarea. Incluye qué cambiaste, cómo se prueba y qué resultado obtuviste. Yisus revisa el PR con Yuki y valida la aplicación en Windows antes de integrarlo.

- Tarot · `feature/tarot-ficticio`. Tirada ficticia, pregunta de reflexión y tres conversaciones de ejemplo.
- Comedia · `feature/comediante`. Comediante del campus con tres anécdotas de prueba.
- Pruebas y guía · `docs/pruebas-demo`. Pasos para probar el chat, el selector y ambos perfiles.
- Yisus · sprites, animaciones y voz con ElevenLabs.

Cada perfil recibe el mensaje como texto y devuelve la respuesta como texto. `Responses.cs` contiene los puntos de entrada de muestra para los dos perfiles. Mantén tu implementación en un archivo de tu módulo y conecta únicamente el método de tu perfil.

La biblioteca y las consultas por página están disponibles en Ajustes → Documentos.

## Ajustes de BAEL

Pulsa Shift + clic derecho sobre el personaje y elige el engrane. La ventana se divide en Personaje, Chat y APIs, y Voz y audio. Guarda al terminar para aplicar los cambios y conservarlos cifrados en tu perfil de Windows.

En Chat y APIs puedes añadir conexiones, elegir proveedor, modelo y clave, activarlas y ordenarlas con Subir/Bajar. Los botones Obtener clave y Ver plan y límites abren las páginas oficiales. Cada proveedor tiene una explicación en el editor.

Opciones sugeridas, revisadas el 18 de noviembre de 2026:

| Proveedor | Modelo sugerido | Uso |
| --- | --- | --- |
| Google Gemini | gemini-2.5-flash-lite | Ayuda general y repaso, con nivel gratuito limitado. |
| Groq | openai/gpt-oss-120b | Conversación y razonamiento, con plan gratuito limitado. |
| OpenRouter | openrouter/free | Respaldo que selecciona un modelo gratuito disponible. |

La disponibilidad y las cuotas dependen de la cuenta. Otros IDs pueden tener costo según el proveedor. Revisa el plan y la facturación antes de usarlos.

Fuentes oficiales

- https://ai.google.dev/gemini-api/docs/pricing
- https://console.groq.com/docs/rate-limits
- https://openrouter.ai/docs/guides/routing/routers/free-router

Si activas el cambio automático, BAEL intenta las conexiones activas en el orden elegido. Una cuota agotada, clave rechazada, error de servicio, respuesta vacía o tiempo de espera agotado hacen que intente la siguiente. El modelo que responde recibe el mensaje y el contexto reciente del modo. BAEL conserva las instrucciones del tutor y muestra qué proveedor respondió. Las conexiones agotadas quedan en espera durante 15 minutos o el plazo Retry-After que indique el proveedor, si es mayor; los errores temporales esperan 30 segundos. Esta espera se conserva durante la sesión.

Sin conexiones disponibles, el chat conserva su guía local. El relevo afecta a la conversación; ElevenLabs sigue configurándose por separado en Voz y audio.

La clave guardada tiene prioridad. Un campo vacío usa la variable local del proveedor si existe: BAEL_GEMINI_API_KEY, BAEL_GROQ_API_KEY o BAEL_OPENROUTER_API_KEY. La clave y el modelo de Gemini de versiones anteriores se conservan como primera conexión al abrir los ajustes nuevos.

Voz y audio permite guardar BAEL_ELEVENLABS_API_KEY y BAEL_ELEVENLABS_VOICE_ID mediante campos locales, seleccionar la salida para la lectura y probar el nivel del micrófono. La entrada sigue siendo una prueba local. El repositorio contiene solo nombres de variables y valores de prueba ficticios.

## Usar sprites propios

Coloca los PNG transparentes en `Sprites/idle/`, `Sprites/walk/` y `Sprites/react/`, junto a `BAEL-UMAN.exe`. Si trabajas desde Visual Studio, coloca esas carpetas dentro de `src/Bael/` y recompila. Usa el mismo tamaño y punto de apoyo en todos los fotogramas. Numéralos como `01.png`, `02.png` y `03.png` para que se reproduzcan en ese orden.

Reinicia BAEL para cargar los archivos. Reposo se repite, caminata se reproduce durante el desplazamiento y reacción se reproduce una vez al pulsar la mascota. Puedes empezar con una sola carpeta o con un solo PNG por estado. Sin sprites de reposo, BAEL muestra la mascota provisional cuando no hay un fotograma para el estado actual.

## Personalidades por modo

Desde Ajustes → Personalidades elige Estudiante, Tarot, Comedia, Sistema, Matemáticas o Inglés. Cada uno empieza con un carácter distinto. Puedes cambiar tono, cantidad de humor (0–3), extensión, nombre de trato y preferencias libres de expresión. Hay un ejemplo local y un botón para restaurar solo el modo seleccionado. Guardar aplica los cambios y los conserva al reiniciar.

Las APIs reciben la personalidad del modo junto con sus instrucciones de función, incluso después de un relevo. La guía local aplica tono, humor y nombre de trato; las preferencias libres y la adaptación completa de la extensión requieren IA en línea. La voz de ElevenLabs lee el texto resultante. Matemáticas conserva la instrucción de guiar con pistas y preguntas.


## Movimiento y conversación

Todos los modos pueden recorrer el área útil de la pantalla principal con trayectorias en dos dimensiones, aceleración suave, cambios de orientación y pausas. El movimiento se pausa al interactuar con el personaje, al abrir el chat o los ajustes y mientras se muestra una respuesta. La opción Mover la mascota permite detenerlo.

Elegir un modo en la rueda cambia el personaje y mantiene el chat oculto. Pulsa el sprite cuando quieras conversar. El chat admite Enter para enviar y Shift + Enter para una línea nueva, muestra mensajes en tarjetas y permite copiar la última respuesta. Junto al personaje aparece una burbuja de hasta 180 caracteres con escritura progresiva. Ver conversación abre el texto completo.

El ejecutable y los accesos directos se llaman BAEL-UMAN y usan el unicornio como icono.

## Bailar con el audio

En Ajustes → Personaje activa Bailar con el audio de la computadora. La opción empieza desactivada y se guarda por usuario. Funciona en los seis modos y con las formas del unicornio.

BAEL consulta el medidor de salida predeterminado de Windows. Cuando el nivel supera el umbral, alterna poses, da saltitos y se balancea. El umbral se puede bajar para música suave. Tras aproximadamente un segundo de silencio vuelve al reposo o a caminar. Al abrir la rueda, arrastrarlo o usar Ajustes se pausa el baile.

El medidor se vuelve a conectar periódicamente para seguir los cambios de audífonos o altavoces predeterminados. La detección usa solo el nivel local, sin guardar ni enviar audio. Cualquier sonido reproducido por esa salida puede activar el efecto, incluida la voz de BAEL.

## Biblioteca de PDF y límites por modo

Abre Ajustes → Documentos y pulsa Añadir PDF. Puedes importar varios archivos con texto seleccionable. Cada archivo muestra su nombre y número de páginas. Elige un modo, activa Limitar este modo solo a sus PDF seleccionados y selecciona los archivos permitidos. Guarda para aplicar. Estudiante empieza limitado a documentos; los otros modos empiezan libres.

En los modos limitados, BAEL busca términos de la pregunta en fragmentos solapados y reúne hasta seis pasajes con archivo y página. Normaliza acentos, algunas terminaciones y unas pocas equivalencias; no es búsqueda semántica general. Si faltan términos, muestra las coincidencias parciales y avisa; si no encuentra respaldo, lo indica. Los accesos Resumen, Explicar, Repaso y Buscar tema sustituyen a las actividades generales mientras el modo está limitado. El resumen local selecciona frases breves de los fragmentos relevantes, no resume todo el documento.

Por defecto, la consulta es local. En Ajustes → Documentos puedes activar por modo «Permitir explicaciones con IA usando fragmentos de estos PDF». Requiere activar también la IA en línea y configurar un proveedor. Al preguntar, se envían la pregunta y hasta seis fragmentos asignados al modo, con archivo y página; no se envía el archivo completo, ni recuerdos ni historial. Los proveedores y el cambio automático de API son los del chat. Desactiva esta casilla para volver a consulta local.

La explicación asistida debe devolver referencias conocidas y citas literales que la app comprueba internamente contra los fragmentos enviados. El chat presenta un resumen breve con referencias, sin repetir todas las citas. Si falla la respuesta, su formato o las citas, se muestran los pasajes locales. Esta comprobación valida las citas, no garantiza que la interpretación de la IA sea correcta: revisa siempre el pasaje y su página. Matemáticas pide pistas sin entregar el resultado final.

Repaso funciona sin API: prepara hasta tres frases del material con una palabra oculta. Responde con la palabra, escribe `pista` o `salir repaso`. Una pregunta nueva, cambiar de modo o abrir Ajustes termina el repaso. Es una práctica de completar palabras, no un examen generado ni una evaluación de comprensión.

Los PDF escaneados sin texto necesitan OCR antes de importarse. Los archivos protegidos o dañados muestran un aviso. Las copias y el índice por página se guardan en el perfil local de Windows. Cancelar los ajustes descarta los archivos nuevos preparados y las asignaciones sin guardar. Quitar de la biblioteca elimina la copia local al guardar y conserva el original.

## Hablar con BAEL

En Ajustes → Voz y audio selecciona el micrófono y configura una clave de ElevenLabs con acceso a Speech to Text. El modelo inicial de transcripción es scribe_v2 y se puede cambiar. La lectura usa además tu ID de voz y acceso a Text to Speech.

Abre el chat y pulsa Hablar. BAEL graba una frase y termina tras una pausa configurable (900 ms por defecto) después de detectar voz, al pulsar Terminar frase o al llegar a 30 segundos. La barra muestra el nivel. El audio se mantiene en memoria y se envía a ElevenLabs para convertirlo en texto. La frase reconocida se muestra y se consulta en el modo actual, respetando sus límites de documentos.

Responder en voz alta a mis mensajes hablados activa la lectura automática de la respuesta. Puedes desactivarlo y usar Leer en voz alta manualmente. La clave debe tener cuota para transcripción y síntesis según tu plan. El micrófono se abre únicamente al pulsar Hablar.

## Diagnosticar la lectura de ElevenLabs

En Ajustes → Voz y audio usa Cargar voces de mi cuenta, selecciona una voz y pulsa Probar voz. El botón genera una frase corta y la reproduce en tu salida seleccionada. Guarda los ajustes que funcionen. También puedes elegir el modelo de lectura: Multilingual v2, Flash v2.5 o un ID compatible con tu voz.

BAEL muestra el estado y el detalle devueltos por ElevenLabs, incluyendo ID de voz inexistente, restricción de plan, cuota o modelo incompatible. El código HTTP por sí solo no identifica la causa. La clave se oculta si aparece en el mensaje del servicio. Cargar voces requiere permiso de lectura de voces en la clave, y listar una voz no garantiza que el plan permita sintetizarla.

## Modos ampliados y sala Gamer

La rueda Shift + clic derecho incluye ocho modos: Estudiante, Tarot, Comedia, Sistema, Matemáticas, Inglés, Libre y Gamer. Cambiar de modo mantiene el chat oculto hasta pulsar el personaje. Cada modo muestra cuatro accesos de actividades en su conversación; las personalidades y las reglas PDF se configuran por modo en Ajustes.

Libre permite conversación natural con las APIs configuradas y una aventura espacial de dos decisiones sin conexión. Matemáticas incluye un reto guiado de tres pasos que pide operaciones, sin entregar la incógnita; Inglés incluye cuatro ejercicios con corrección. Tarot tiene cartas ficticias aleatorias y Comedia un pequeño repertorio local. El resto de accesos aporta una guía local o envía una propuesta a la IA, respetando la restricción a documentos antes de procesar actividades. El modo Estudiante conserva su restricción PDF predeterminada. Para finalizar una actividad interactiva escribe `salir actividad`.

Gamer abre una ventana de juegos al pulsar Vasos o Póker. Incluye mezcla animada de tres vasos y póker de exhibición de cinco cartas con cambio de hasta tres. Se anuncia de forma permanente que BAEL siempre gana: en vasos mueve la pelota fuera de la elección y en póker reserva una escalera real. El evaluador identifica la mano del jugador; no es un casino ni una IA competitiva de póker. Todas las apuestas son ficticias: saldo inicial 1.000, apuestas 10/50/100 y recarga manual al llegar a cero. El saldo dura mientras la ventana permanece abierta, sin compras ni dinero real. Las bromas se pueden apagar con Humor 0 de la personalidad Gamer; sus instrucciones personalizadas se usan en la conversación por API.

Ocho fotogramas transparentes nuevos (`Sprites/desktop/free-0.png` a `free-3.png` y `gamer-0.png` a `gamer-3.png`) se integran en la rueda y en las animaciones del escritorio. Generados con el generador de imágenes integrado a partir del chico existente: cuatro poses de charla con sudadera lavanda y cuatro de jugador con chaqueta, auriculares, cartas y ficha ficticia; fondo transparente, misma identidad chibi. La hoja se extrajo sin fondo y se separó en celdas de 320×256.

Las pruebas de Windows verifican los ocho accesos de modo, la rueda, sus 44 fotogramas, progresión de actividades, saldo y recarga, exclusión de cartas duplicadas, clasificación de manos y una ronda de póker desde la interfaz, además del instalador y las pruebas anteriores.


## Transparencia, atajos globales y baile de unicornio

En Ajustes → Pantalla y teclas el fondo del chat puede hacerse transparente entre 0% y 65% (35% inicial). El texto permanece opaco. El chat tiene borde redondeado, se mueve arrastrando el encabezado y se redimensiona desde la esquina inferior derecha. La rueda usa una miniatura recortada al área visible del PNG y una cuadrícula centrada para la imagen y su nombre.

Atajos iniciales: **Ctrl+Alt+V** para comenzar la escucha sin abrir el chat (otra pulsación termina la frase o cancela la transcripción), y **Ctrl+Alt+B** para activar/desactivar el baile con audio. Son pulsaciones, no requieren mantener la tecla. Usa un modificador Ctrl/Alt/Win o una tecla F excepto F12; se pueden cambiar en Ajustes y borrar con Supr/Retroceso. Se rechazan combinaciones repetidas. Los atajos se suspenden al editar ajustes, se vuelven a registrar al cerrar y se liberan al salir. Si otro programa ocupa una combinación, BAEL avisa en su burbuja. La entrada de voz sigue necesitando la clave y permisos de ElevenLabs; sus estados y la respuesta se muestran junto al personaje. La detección del audio se pausa mientras se captura voz o se interactúa con el personaje.

En cualquier modo, el audio del dispositivo predeterminado activa una transición mágica al unicornio. La rutina combina ocho sprites (`Sprites/desktop/dance-0.png` a `dance-7.png`) con zapateo, disco, pasos cruzados, salto, vuelta completa, marometa y giro lateral. Al terminar el audio se espera un breve margen de silencio, se muestra la transformación inversa y reaparece el aspecto anterior. Cambiar de modo o apagar la opción restaura el modo seleccionado. La ventana transparente de la mascota reserva más espacio para las rotaciones sin recortarlas.

Los sprites se generaron con el generador integrado usando el unicornio existente como referencia: hoja transparente 4×2, misma cabeza blanca/cuerno plateado/ojos azules/uniforme blanco con franjas naranjas y zapatos negros, ocho poses de baile aisladas. Se separaron en ocho celdas de 320×256 conservando el canal alfa. La animación suave se calcula a partir del tiempo transcurrido, en el temporizador de movimiento.

Las pruebas de Windows comprueban registro nativo, entrega de mensajes y conflictos/liberación de atajos con ventanas de prueba, las transiciones de baile, los límites de la coreografía, los nuevos PNG y el centrado de las miniaturas. Las teclas físicas, el dispositivo de sonido y el acabado visual todavía deben comprobarse en la PC del usuario.

Referencias de plataforma: [RegisterHotKey](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey), [UnregisterHotKey](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-unregisterhotkey) y [AllowsTransparency de WPF](https://learn.microsoft.com/dotnet/api/system.windows.window.allowstransparency).


## Más baile, tamaño y conversación espontánea

Se añaden doce sprites transparentes (`Sprites/desktop/dance-8.png` a `dance-19.png`) al repertorio anterior: moonwalk, robot, dab, running man, pasos de salsa y breakdance. La rutina usa las veinte poses en catorce estilos, con un orden que se mezcla al iniciar y cambia en cada ciclo. Las doce poses nuevas se generaron con el generador integrado: misma identidad del unicornio existente, hoja 4×3, poses emparejadas de cada baile, fondo alfa transparente y celdas separadas de 320×256.

Ctrl+Alt+↑ aumenta el personaje y Ctrl+Alt+↓ lo reduce, en pasos de 10%. Ambos atajos se pueden cambiar o desactivar en Ajustes → Pantalla y teclas. Allí también hay un deslizador de tamaño de 60% a 180%. La mascota se escala completa conservando proporciones, con límites al área útil de pantalla y posición corregida para quedar dentro; se guarda la preferencia. La rueda y las burbujas conservan su tamaño de lectura.

En Ajustes → Compañía se puede activar la charla espontánea del modo Libre (activa inicialmente), elegir un intervalo de 30 a 900 segundos (180 por defecto) y activar la lectura con ElevenLabs (apagada inicialmente). El primer comentario puede aparecer unos 20 segundos después de entrar en Libre si no hay interacción. Sale en la burbuja y queda en el chat sin abrirlo. Se pausa al escribir, grabar, escuchar una respuesta, abrir Ajustes, jugar la aventura de Libre o bailar. Si Libre está limitado a PDF, no inicia comentarios.

Con IA en línea, los comentarios aplican la personalidad e instrucciones de Libre, sin inventar que observan el escritorio o recuerdan hechos no compartidos. Usan las APIs activas y su cuota. Sin conexión o si no llega la respuesta a tiempo, hay preguntas, curiosidades estables y ocurrencias locales ajustadas al trato, tono y presencia de humor; el repertorio local no interpreta instrucciones libres. La voz opcional consume cuota de ElevenLabs. Cambiar de modo, escribir o hablar cancela un comentario pendiente. El siguiente mensaje del usuario incluye el comentario espontáneo como contexto para que la IA pueda continuar el hilo.

Las pruebas verifican los cuatro atajos globales y su liberación, persistencia de los nuevos ajustes, escalas mínima/máxima y proporciones, uso de las veinte poses de baile, ritmo de comentarios, bloqueo al escribir o limitar a PDF y publicación local de un comentario sin abrir la ventana de chat. Audio y apariencia deben comprobarse en la PC real.


## Micrófono más ágil, temas y comandos locales

La apertura y el procesamiento de audio se realizan fuera del hilo de interfaz. BAEL muestra Preparando micrófono y solo indica Te escucho después de iniciar la captura. Usa captura WASAPI por eventos con búfer solicitado de 30 ms y mide las muestras recibidas, sin depender del indicador global de Windows. El nivel mínimo inicial baja a 0,4%; la pausa de fin de frase es de 900 ms por defecto, configurable de 600 a 2.200 ms en Ajustes → Voz y audio. El nivel se puede ajustar de 0,1% a 5%. Sin voz, termina a los 8 segundos; una frase conserva el máximo de 30 segundos y el control manual de terminar/cancelar.

Se conserva un margen de 250 ms alrededor de la voz, se recorta el silencio sobrante y se convierte a WAV mono PCM16 a 16 kHz antes de enviarlo a ElevenLabs. La transcripción sigue comenzando después de grabar la frase: no es transcripción en streaming. El tamaño de una señal de prueba estéreo float de 48 kHz y un segundo se reduce aproximadamente de 384 KB de muestras a 32 KB, sin acortar la duración. El tiempo real depende del micrófono, su controlador, la red y ElevenLabs.

En Ajustes → Pantalla y teclas se elige Claro u Oscuro para la ventana de configuración. La vista previa cambia al instante; Guardar recuerda la elección y Cancelar conserva la anterior. Los colores de paneles, texto, campos, listas, pestañas, selección y errores usan recursos de tema. El cambio se limita a Ajustes, manteniendo el chat translúcido y la mascota. La barra de título solicita el estilo oscuro de Windows cuando está disponible.

En cualquier modo, por texto o por una frase transcrita, se puede preguntar qué puedes hacer, qué funciones tienes, ayuda o qué modos tienes. BAEL responde con sus funciones reales, actividades, límites y atajos configurados. También hay un botón ¿Qué puedo hacer? en el chat.

Los cambios de modo son locales: cambia al modo Inglés, quiero cambiar el modo a Sistema, ponte en modo Libre o activa el modo Gamer. Se aceptan acentos, mayúsculas y algunas variantes naturales. Si no reconoce el destino, muestra los ocho modos y no cambia a uno inventado. Una negación o una petición de traducción no se interpreta como cambio. Los comandos se atienden antes de las actividades, las APIs y la consulta PDF; la ayuda de la app no altera las restricciones del material. Cambiar de modo detiene la actividad guiada anterior y no abre por sí solo el chat.

Pruebas añadidas: ayuda en los ocho modos incluso restringidos a PDF; cambio de modo desde el recorrido real de envío; variantes y negaciones; detección de voz suave, pausa configurable y espera máxima sin voz; medición PCM/float; conversión con formato, duración y tamaño verificables; colores de ambos temas. No se usan micrófonos ni claves reales en las pruebas automáticas. Queda comprobar con voz y pantalla en el equipo del usuario.


## Interrumpir a BAEL, gestos y tiempos de respuesta

Pulsa Hablar o Ctrl+Alt+V mientras BAEL genera o reproduce su voz para detenerla e iniciar el micrófono. Se mantienen los controles de fin de frase y cancelación. La interrupción es por botón o atajo: no escucha continuamente para detectar si lo interrumpes hablando. Debe haber una clave de transcripción configurada y el cuadro de mensaje vacío. Una generación cancelada que termine tarde se descarta y no puede borrar ni reproducirse sobre una solicitud posterior.

El botón Detener cancela la captura, transcripción, respuesta de IA/PDF o voz actual. Los resultados cancelados no se publican como respuestas nuevas. Cambiar de modo cancela la respuesta pendiente; abrir Ajustes detiene también el audio y la captura para evitar mezclar las pruebas de dispositivos con una conversación.

La mascota muestra estados de abrir micrófono, escuchar, transcribir, pensar, preparar voz y hablar. Los gestos usan los cuatro fotogramas existentes de su aspecto actual y movimientos suaves de cabeza/cuerpo; conservan el traje de cada modo. Una etiqueta breve sobre el personaje indica el estado. Durante estos estados pausa el baile y el movimiento libre; no baila con su propia lectura. Son gestos de actividad, sin sincronización fonética con los labios.

En el chat, Tiempos de esta interacción muestra mediciones locales de apertura del micro, grabación, transcripción, consulta PDF/IA y preparación de voz. Grabar incluye el tiempo que habla el usuario; las otras etapas muestran la espera de ese paso. La actividad en curso incluye un contador. Copiar tiempos copia solo estas mediciones, sin conversación ni claves. No es una mejora cuantificada del proveedor: permite localizar la demora real en cada equipo.

Pruebas automáticas añadidas: botón de Hablar iniciando una captura simulada mientras cancela TTS, descarte de audio que llega tarde, protección de una solicitud nueva frente al cierre de una anterior, estados/poses válidos en los fotogramas existentes y etiquetas de las etapas medidas. Se prueban sin micrófono, altavoces ni claves reales; la interrupción física y el sonido deben comprobarse en la PC del usuario.


## Historial persistente y recuerdos elegidos

El botón Memoria del chat y Ajustes → Memoria abren el gestor de historiales y recuerdos. Cada modo tiene una conversación separada; al cambiar de modo se muestra su historial y al volver a abrir BAEL se recupera lo conservado. La recuperación no abre el chat ni reproduce audio. Se mantienen los últimos 100 mensajes por modo, con un límite de 4.000 caracteres por mensaje. La consulta a APIs puede reutilizar hasta 12 turnos recientes del mismo modo.

El gestor permite buscar dentro de un modo, borrar su historial, crear/editar/desactivar/borrar recuerdos y restablecer toda la memoria. Cada modo admite 20 notas de hasta 500 caracteres, con un máximo de seis activas. Son notas escritas y elegidas por el usuario, sin extracción automática de información personal. Los cambios del gestor se aplican al instante. Borrar un historial elimina su contexto y conserva las notas elegidas; borrar toda la memoria elimina ambas cosas. Mientras el gestor esté abierto no comienza charla espontánea ni se abre el micrófono con el atajo.

Hay tres opciones independientes: conservar historial al cerrar, compartir el contexto reciente del modo con los proveedores que se consulten y usar recuerdos activos elegidos. Al apagar y guardar la conservación del historial se quitan los mensajes/contextos del archivo local, manteniendo la sesión visible y las notas elegidas. Con IA activa, los recuerdos permitidos se añaden como datos del usuario, sin sustituir las instrucciones del modo. En modos limitados a PDF no se inyectan recuerdos ni historial a la IA; la explicación documental opcional recibe únicamente la pregunta y los fragmentos seleccionados. Sin API se puede consultar el historial, pero el repertorio de respuestas locales no interpreta recuerdos para conversar libremente.

La memoria se guarda en `%LOCALAPPDATA%/BAEL/memory.dat`, protegida con DPAPI para la cuenta actual de Windows, separada del archivo de claves y ajustes. El guardado usa instantáneas acotadas, un escritor en segundo plano y reemplazo de archivo para evitar bloquear la interfaz o intercalar escrituras. Al cerrar se espera a que termine el guardado pendiente. Si un archivo no se puede leer, se conserva sin sobrescribirlo y se indica cómo restablecerlo. Un error de guardado se muestra en el chat y en el gestor. El borrado se aplica a la memoria local de BAEL.

Pruebas añadidas: escritura/lectura cifrada, ráfagas de guardado, límites de tamaño del historial, preservación de archivo ilegible, recuperación al reabrir una ventana real de chat, separación entre modos, borrado que no reaparece, edición/desactivación de notas en el gestor y exclusión de contexto/recuerdos según los controles y el límite PDF. Las pruebas de la app usan memoria temporal aislada y no modifican el historial del usuario.


## Vista de exposición y alumnos

BAEL arranca siempre con el unicornio y el chat de Estudiante. El personaje permanece en su lugar, conserva parpadeos/gestos y se puede arrastrar; pulsa sobre él para abrir el chat. En esta vista no camina, no cambia de disfraz ni baila por audio. El chat tiene fondo sólido. La consulta del alumno se limita a los PDF asignados, incluso si la preferencia del modo Estudiante se había dejado libre.

Ajustes y Desbloquear BAEL están tanto junto al unicornio como en el chat. Desbloquear abre el modo Libre y habilita los ocho modos y la rueda; Volver a alumnos (o Alumnos junto al personaje) restaura Estudiante. Ctrl + Shift + L alterna las vistas mientras BAEL o su chat tienen el foco; es un atajo local, no global. Cada nuevo arranque vuelve a la vista de alumnos. Es una interfaz para la exposición, no un control de acceso con contraseña. Los ajustes de movimiento y baile vuelven a aplicarse al desbloquear.

Subir PDF abre directamente Ajustes → Documentos para el modo actual. Importa, selecciona y guarda tus archivos antes de preguntar. El chat responde con hasta tres frases relevantes de aproximadamente 240 caracteres cada una, con archivo y página, sin volcar la página completa. Es un resumen extractivo local; no inventa una definición fuera del material. La explicación con IA sigue siendo opcional y requiere conexión y un proveedor configurado: solicita hasta 90 palabras, comprueba las citas literales internamente y muestra la explicación con las referencias; si no pasa la validación, conserva el resumen local. Los PDF escaneados requieren OCR previo.

Las pruebas de Windows incluyen el bloqueo de cambios de modo por comandos, el acceso de ida y vuelta a alumnos, fondo opaco, unicornio inmóvil, límites de longitud y referencias del resumen, además de las pruebas existentes de arranque e instalación. Falta validar voz y apariencia en la PC de la exposición con sus PDF reales.


## Centro del sistema integrado

Desbloquea BAEL, elige Sistema y pulsa Centro del sistema. También tiene accesos a Buscar archivos, Liberar espacio y RAM y ampliación. Puedes escribir `diagnóstico`, `buscar archivos`, `liberar espacio`, `ampliar RAM`, `salud del disco` o `revisar componentes`. Estos comandos abren la sección; nunca borran ni reparan automáticamente. La vista de alumnos y un modo limitado a PDF no permiten abrir las herramientas técnicas. Volver a alumnos o cambiar a otro modo cierra el panel y cancela el trabajo pendiente.

El panel funciona sin clave de IA. Muestra uso local de CPU y RAM cada segundo, una gráfica de CPU de hasta 60 muestras, espacio de unidades locales/removibles, inventario de CPU, placa, BIOS, GPU/controladores y módulos RAM. Revisar hardware ejecuta un lector fijo de PowerShell de Windows sin ventana, con consultas CIM y Storage de solo lectura, cancelación y un límite de espera de 35 segundos. No instala programas ni cambia permisos, BIOS, registro o controladores. No envía los nombres de archivos, rutas ni el inventario a los modelos del chat.

Discos presenta estado declarado, tipo/bus, tamaño y, si el controlador y los permisos lo permiten, temperatura, desgaste consumido, horas y errores no corregidos. Un dato ausente se muestra como no disponible, no como un disco sano. El desgaste no permite predecir años restantes; Healthy no garantiza ausencia de fallos. USB, RAID y algunos NVMe pueden no exponer contadores. Se eligieron los contadores nativos de Windows para esta integración, sin requerir otra aplicación gráfica.

RAM presenta ubicación, capacidad, DDR identificado por SMBIOS, DIMM/SO-DIMM cuando está declarado, fabricante, referencia y velocidad configurada en MHz y campo Speed nominal sin conversión de WMI. Las ubicaciones libres y el máximo de memoria son declaraciones del firmware: no demuestran ranuras accesibles, RAM reemplazable ni compatibilidad. Comprueba el manual del modelo exacto antes de comprar. Componentes muestra incidencias declaradas por Plug and Play; no certifica salud eléctrica, estabilidad o temperaturas de CPU/GPU. Un resultado vacío o un estado OK no sustituye pruebas específicas de hardware.

Buscar archivos recorre una unidad o carpeta por parte del nombre sin leer su contenido; Archivos grandes conserva los 200 mayores encontrados desde 10 MB. Se omiten enlaces, uniones y zonas inaccesibles. El recorrido es parcial al llegar a 200.000 elementos, 30 segundos entre lecturas o 500 coincidencias por nombre; lo indica al terminar. Mostrar carpeta abre el Explorador seleccionando el archivo, sin ejecutarlo. No se borran archivos desde los resultados.

Liberar espacio analiza solo la carpeta temporal del usuario y ofrece una vista previa con casillas. Excluye archivos con escritura/acceso en los últimos siete días, protegidos, enlaces y carpetas cuyo nombre empieza por BAEL. Limpiar selección elimina definitivamente los temporales elegidos que vuelvan a pasar las comprobaciones. Se usa un identificador exclusivo del archivo y se verifica ruta final, edad, tamaño, fecha de modificación, atributos y enlaces antes de marcarlo para borrar. Los archivos en uso o cambiados se omiten; no se borran directorios. No toca Documentos, Descargas, Papelera ni carpetas de Windows. Cancelar conserva los cambios ya realizados y muestra el resultado parcial. El total retirado es tamaño de archivos, no una promesa de espacio físico recuperado; el panel vuelve a leer el espacio libre real.

Pruebas: panel y recomendaciones con datos de prueba; datos desconocidos; límites de alumnos; comandos; selección, archivos cambiados/en uso, rutas fuera de temporales, archivos protegidos, exclusiones BAEL y cancelación; enlaces simbólicos cuando Windows permite crearlos; validación de sintaxis del lector de PowerShell y una prueba separada de lectura real del inventario en el equipo de Windows de integración. La disponibilidad de contadores y los datos SMBIOS deben comprobarse en la PC real.

Referencias del lector: [Storage reliability counters](https://learn.microsoft.com/en-us/powershell/module/storage/get-storagereliabilitycounter), [memoria física](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-physicalmemory), [ubicaciones y máximo declarado de RAM](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-physicalmemoryarray), [incidencias PnP](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-pnpentity).


## Aula de inglés por niveles

Desbloquea BAEL → Inglés → Aula por niveles. También puedes escribir `aula de inglés`, `practicar escritura`, `practicar lectura`, `comprensión auditiva` o `practicar conversación`. La vista de alumnos y un modo Inglés limitado a PDF no abren el aula. Cambiar de modo, volver a alumnos o cerrar el aula detiene audio, grabación y solicitudes pendientes. Abrir Ajustes cierra el aula para evitar utilizar claves o dispositivos anteriores.

Hay cuatro niveles de práctica (A1, A2, B1 y B2), con dos temas por nivel: rutina y cafetería; fin de semana y viaje; problemas de trabajo y hábitos de estudio; entorno y tecnología. Cada tema tiene objetivo, consejo, consigna de escritura, texto de lectura, escucha de exactamente 50 palabras con tres preguntas y un escenario de tres objetivos de conversación. Puedes elegir cualquier nivel. Continuar ruta dirige a la siguiente práctica pendiente. Las marcas de actividad no acreditan un nivel oficial: escribir cuenta una revisión, lectura al menos 70% de coincidencia textual, escucha al menos 70% sin transcripción mostrada y conversación sus tres objetivos guiados.

Escritura subraya según el diccionario inglés de Windows (si está disponible), muestra opciones de corrección y aplica reglas locales de errores frecuentes. Selecciona una observación y una opción antes de Aplicar sugerencia elegida. Si editaste el texto desde la revisión, se exige revisarlo otra vez para no reemplazar un fragmento equivocado. No es un corrector completo como Grammarly ni garantiza detectar todos los errores. Revisión con IA es opcional: requiere IA en línea, envía solo el texto actual y el nivel a los proveedores configurados y valida que cada fragmento propuesto exista una sola vez. Las propuestas nunca se aplican automáticamente. Un fallo deja disponible la revisión local.

Lectura permite escuchar el modelo, grabar hasta 30 segundos y comparar el texto con una transcripción en inglés. Usa el micrófono configurado y ElevenLabs para transcribir; puedes terminar la frase o detener la actividad. Las palabras coincidentes, ausentes, cambiadas y añadidas se muestran por colores con su detalle. Puedes editar o escribir el intento; en ese caso se identifica como comparación de texto. No se evalúan fonemas, acento, ritmo ni entonación: un error de reconocimiento puede producir diferencias aunque hayas pronunciado bien. La puntuación de coincidencia no es una nota de pronunciación.

Escucha reproduce un texto de 50 palabras, oculto inicialmente, y habilita las preguntas después de reproducirlo. Cada respuesta muestra si es correcta, la solución y una explicación. Se pueden repetir los audios. Mostrar texto cambia ese intento a práctica asistida; desmarcarlo después no borra que se utilizó apoyo. Nuevo intento prepara otra práctica sin borrar el historial. Las opciones se mezclan para no fijar siempre la misma posición de la respuesta correcta.

Conversación tiene ocho escenarios, elegidos por nivel/tema, con respuestas escritas o grabadas, pista, lectura de BAEL y reinicio. La guía sin conexión busca frases de al menos cuatro palabras con alguna estructura/palabra del objetivo; no es una evaluación semántica, de gramática completa ni de fluidez. Puedes activar Usar IA para conversar: envía mensajes de ese escenario (según el control de contexto del chat), nivel y objetivo al proveedor; no incluye otros temas ni recuerdos elegidos del chat. Si la API falla, se mantiene la guía local. Se conservan hasta veinte mensajes del escenario activo y los turnos anteriores se consultan en los intentos recientes.

Voz: Windows local necesita una voz inglesa SAPI instalada y usa la salida predeterminada de Windows; no requiere API. ElevenLabs utiliza la clave, voz y salida configuradas en BAEL. El aula no instala voces automáticamente. La transcripción siempre utiliza ElevenLabs con `language_code=eng`; no es reconocimiento de voz local. Las grabaciones permanecen en memoria y no se guardan como archivos de audio. El atajo global de Hablar, con el aula visible, graba lectura o conversación según la pestaña; otra pulsación termina la frase.

Borradores, respuestas, feedback, nivel/tema, selección de voz, conversaciones y marcas de actividad se guardan localmente en `%LOCALAPPDATA%/BAEL/english-study.dat`, cifrados con DPAPI para el usuario de Windows. Se conservan los 300 intentos recientes; las marcas de actividad quedan separadas de ese límite. Este progreso es independiente del historial general del chat. El guardado se hace al pausar la escritura, cambiar de tema, evaluar o cerrar. Si el archivo es ilegible se conserva y se bloquea la sobrescritura, indicando el problema en el aula.

Pruebas: 50 palabras en los ocho textos; claves y explicaciones de preguntas; alineación por palabras; reglas locales y propuestas IA ambiguas; selección de sugerencias en la ventana, apoyo de texto, separación entre niveles, guardado/recuperación cifrados y conservación de un archivo ilegible; envío de idioma inglés en una transcripción simulada; acceso desde el modo y cierre al cambiarlo. No se utilizan claves reales, micrófonos ni reproducción de sonido en las pruebas automáticas. La voz instalada, los dispositivos y el reconocimiento deben probarse en la PC real.
