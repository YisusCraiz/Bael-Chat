# BAEL

Aplicación de escritorio para Windows desarrollada en C# y WPF.

## Abrir en Visual Studio

1. En Windows, abre **Visual Studio Installer** y elige **Modificar** en Visual Studio 2022 (17.8 o posterior).
2. Marca **Desarrollo de escritorio de .NET**. Comprueba que tienes el **SDK de .NET 8** con `dotnet --list-sdks`. Si no aparece una versión 8.0, instala el [SDK de .NET 8](https://dotnet.microsoft.com/es-es/download/dotnet/8.0).
3. Clona el repositorio con GitHub Desktop y selecciona `feature/base-wpf` mientras el PR inicial esté abierto. Cuando se integre, podrás usar `main`.
4. Abre `Bael.sln` en Visual Studio y pulsa **F5**. Esta versión usa WPF de .NET 8 y no requiere paquetes NuGet externos.

## Descargar y ejecutar desde GitHub

1. Inicia sesión en GitHub y abre [Compilar BAEL en Windows](https://github.com/YisusCraiz/Bael-Chat/actions/workflows/windows-build.yml).
2. Abre la compilación exitosa más reciente y descarga **BAEL-Windows-x64** desde **Artifacts**.
3. Extrae todo el ZIP y abre `Bael.exe` en Windows x64. El paquete incluye el runtime de .NET.

## Probar el prototipo

- Con el chat oculto y el cursor fuera de la mascota, espera unos segundos para verla caminar dentro de la ventana. Cuando abres el chat se detiene. El parpadeo de reposo continúa con el chat abierto.
- Pulsa la mascota **BAEL** una vez para abrir el chat. La mascota parpadea al pulsarla. Usa **Ocultar chat** para plegarlo y conserva la conversación abierta al volver.
- Pulsa **BAEL** tres veces seguidas en un segundo para abrir el selector. Elige Tarot o Comedia, escribe un mensaje y pulsa **Enviar** o **Enter**.
- Elige **Estudiante** en el selector para volver al modo inicial. Las respuestas de Tarot y Comedia son de muestra.

## Trabajar en equipo

Crea una rama desde `main` actualizado y abre un PR por tarea. Incluye qué cambiaste, cómo se prueba y qué resultado obtuviste. Yisus revisa el PR con Yuki y valida la aplicación en Windows antes de integrarlo.

- Tarot · `feature/tarot-ficticio`. Tirada ficticia, pregunta de reflexión y tres conversaciones de ejemplo.
- Comedia · `feature/comediante`. Comediante del campus con tres anécdotas de prueba.
- Pruebas y guía · `docs/pruebas-demo`. Pasos para probar el chat, el selector y ambos perfiles.
- Yisus · sprites, animaciones y voz con ElevenLabs.

Cada perfil recibe el mensaje como texto y devuelve la respuesta como texto. `Responses.cs` contiene los puntos de entrada de muestra para los dos perfiles. Mantén tu implementación en un archivo de tu módulo y conecta únicamente el método de tu perfil.

La consulta de PDF y las citas por página quedan para una fase posterior.

## Configurar la voz opcional

1. En Windows abre **Editar las variables de entorno de tu cuenta**.
2. Crea `BAEL_ELEVENLABS_API_KEY` con tu clave de ElevenLabs y `BAEL_ELEVENLABS_VOICE_ID` con el ID de una voz disponible en tu cuenta. Guarda ambos valores solo en la PC de prueba.
3. Cierra y vuelve a abrir BAEL. Si usas F5, reinicia Visual Studio para cargar las variables.
4. Envía un mensaje en Tarot o Comedia y pulsa **Leer en voz alta**. BAEL envía a ElevenLabs el texto de su última respuesta solo cuando pulsas ese botón. Puedes cancelar la solicitud o detener la reproducción. Si falta configuración o falla el servicio, el chat sigue funcionando.

## Usar sprites propios

Coloca los PNG transparentes en `Sprites/idle/`, `Sprites/walk/` y `Sprites/react/`, junto a `Bael.exe`. Si trabajas desde Visual Studio, coloca esas carpetas dentro de `src/Bael/` y recompila. Usa el mismo tamaño y punto de apoyo en todos los fotogramas. Numéralos como `01.png`, `02.png` y `03.png` para que se reproduzcan en ese orden.

Reinicia BAEL para cargar los archivos. Reposo se repite, caminata se reproduce durante el desplazamiento y reacción se reproduce una vez al pulsar la mascota. Puedes empezar con una sola carpeta o con un solo PNG por estado. Sin sprites de reposo, BAEL muestra la mascota provisional cuando no hay un fotograma para el estado actual.
