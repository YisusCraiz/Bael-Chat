# BAEL

Aplicación de escritorio para Windows desarrollada en C# y WPF.

## Ejecutar la base

1. Clona este repositorio con GitHub Desktop.
2. En Windows, instala Visual Studio con la carga de trabajo **Desarrollo de escritorio de .NET** y el SDK de .NET 8.
3. Abre `Bael.sln` y pulsa **F5**.

## Probar el prototipo

- Pulsa **BAEL** una vez para abrir el chat.
- Pulsa **BAEL** tres veces seguidas en un segundo para abrir el selector. Elige Tarot o Comedia, escribe un mensaje y pulsa **Enviar** o **Enter**.
- Elige **Estudiante** en el selector para volver al modo inicial. Las respuestas de Tarot y Comedia son de muestra.

## Trabajar en equipo

Crea una rama desde `main` actualizado y abre un PR por tarea. Incluye qué cambiaste, cómo se prueba y qué resultado obtuviste. Yisus revisa el PR con Yuki y valida la aplicación en Windows antes de integrarlo.

- Tarot · `feature/tarot-ficticio`. Tirada ficticia, pregunta de reflexión y tres conversaciones de ejemplo.
- Comedia · `feature/comediante`. Comediante del campus con tres anécdotas de prueba.
- Pruebas y guía · `docs/pruebas-demo`. Pasos para probar el chat, el selector y ambos perfiles.
- Yisus · sprites, animaciones y voz con ElevenLabs.

Cada perfil recibe el mensaje como texto y devuelve la respuesta como texto. `Responses.cs` contiene los puntos de entrada de muestra para los dos perfiles. Mantén tu implementación en un archivo de tu módulo y conecta únicamente el método de tu perfil.

La consulta de PDF y las citas por página quedan para una fase posterior. El botón de voz se habilitará al integrar ElevenLabs. Guarda cualquier clave de voz en la configuración local de la PC de demostración.
