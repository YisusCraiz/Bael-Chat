# BAEL

Aplicación de escritorio para Windows desarrollada en C# y WPF.

## Abrir en Visual Studio

1. En Windows, abre **Visual Studio Installer** y elige **Modificar** en Visual Studio 2022 (17.8 o posterior).
2. Marca **Desarrollo de escritorio de .NET**. Comprueba que tienes el **SDK de .NET 8** con `dotnet --list-sdks`. Si no aparece una versión 8.0, instala el [SDK de .NET 8](https://dotnet.microsoft.com/es-es/download/dotnet/8.0).
3. Clona el repositorio con GitHub Desktop y selecciona `feature/base-wpf` mientras el PR inicial esté abierto. Cuando se integre, podrás usar `main`.
4. Abre `Bael.sln` en Visual Studio y pulsa **F5**. Visual Studio restaura automáticamente NAudio y el paquete de protección de datos de Windows al compilar.

## Descargar y ejecutar desde GitHub

1. Inicia sesión en GitHub y abre [Compilar BAEL en Windows](https://github.com/YisusCraiz/Bael-Chat/actions/workflows/windows-build.yml).
2. Abre la compilación exitosa más reciente y descarga **BAEL-Windows-x64** desde **Artifacts**.
3. Extrae todo el ZIP y abre `Bael.exe` en Windows x64. El paquete incluye el runtime de .NET.

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

Haz clic izquierdo sobre el sprite y abre el engrane que aparece debajo. Los cambios guardados se aplican en esa sesión y se conservan al reiniciar. BAEL guarda los ajustes cifrados con la cuenta de Windows en `%LOCALAPPDATA%\BAEL`.

- **General:** mantener la ventana encima, activar o desactivar la caminata y ajustar el volumen.
- **Chat · Gemini:** activa el chat en línea para Tarot y Comedia, pega tu clave y elige el modelo. Si dejas el modelo vacío se usa `gemini-2.5-flash`. El modo Estudiante ofrece ayuda general de estudio; las consultas de PDF siguen pendientes.
- **Voz · ElevenLabs:** pega la clave y el ID de una voz. Elige la salida para la lectura, o usa la predeterminada de Windows. Puedes seleccionar un micrófono y probar su nivel localmente. La entrada de micrófono no se usa para transcribir ni se envía a ElevenLabs. Envía un mensaje y pulsa **Leer en voz alta**.

Los campos guardados tienen prioridad. Si dejas un campo vacío, BAEL usa la variable local correspondiente cuando existe: `BAEL_GEMINI_API_KEY`, `BAEL_ELEVENLABS_API_KEY` o `BAEL_ELEVENLABS_VOICE_ID`. El repositorio no contiene claves de API predeterminadas.

## Usar sprites propios

Coloca los PNG transparentes en `Sprites/idle/`, `Sprites/walk/` y `Sprites/react/`, junto a `Bael.exe`. Si trabajas desde Visual Studio, coloca esas carpetas dentro de `src/Bael/` y recompila. Usa el mismo tamaño y punto de apoyo en todos los fotogramas. Numéralos como `01.png`, `02.png` y `03.png` para que se reproduzcan en ese orden.

Reinicia BAEL para cargar los archivos. Reposo se repite, caminata se reproduce durante el desplazamiento y reacción se reproduce una vez al pulsar la mascota. Puedes empezar con una sola carpeta o con un solo PNG por estado. Sin sprites de reposo, BAEL muestra la mascota provisional cuando no hay un fotograma para el estado actual.
