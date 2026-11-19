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

- **Estudiante:** organización y explicaciones de estudio. PDF pendiente.
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

La consulta de PDF y las citas por página quedan para una fase posterior.

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
