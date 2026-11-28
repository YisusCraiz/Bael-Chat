# Animación Six seven

Atajo inicial: Ctrl + Alt + 6 de la fila superior (`Ctrl+Alt+D6` en Ajustes → Pantalla y teclas). Repetir el atajo detiene el baile. Escape con BAEL enfocado también lo detiene. Se interrumpe al escuchar/responder o cambiar de modo. Funciona en la vista de alumnos y en los modos libres, sin música, API ni audio adicional.

El atlas transparente se guarda en `src/Bael/Sprites/desktop/six-seven-atlas.png`: 1536 × 1024, tres columnas por dos filas. WPF recorta seis celdas en memoria; conserva el alfa original. Los números 6 y 7 son capas de texto independientes, con movimiento lateral y vertical coordinado con las manos. La animación no cambia el modo ni guarda un traje distinto.

## Referencias y creación del recurso

Se usaron como referencia la hoja de baile aportada por el usuario y el unicornio ya existente. Imagen creada con generación integrada; sin audio generado.

Prompt de generación:

Use case: stylized-concept. Asset type: ONE transparent animation sprite atlas for BAEL desktop mascot. Reference1 shows the mascot dance sheet, ignore all phone screenshot UI/black background. Reference2 shows exact mascot identity. Create ONE sprite sheet in a strict 3 columns by 2 rows equal grid, six full body frames in reading order, same size character and same foot baseline in each cell. All six frames show the SAME white unicorn mascot with long rounded muzzle, horn, pink inner ears, gray long sleeve sweatshirt, white track pants with orange outer stripe, big black hoof shoes; SAME clean outlined shaded cartoon style as references. Add cool BLACK SUNGLASSES covering the eyes, identical sunglasses all frames. Animate the playful 'six seven' palms-up balancing gesture: both elbows bent, forearms reaching to each side and open palms facing UP. Frame1 image-left palm high/image-right palm low, body leaning slightly left; Frame2 hands nearing center height, small knee bounce; Frame3 image-left palm low/image-right palm high, lean slightly right; Frame4 same right-high at gentle peak; Frame5 both palms middle during bounce; Frame6 left-high again to loop into frame1. Smooth subtle six-frame cycle, cheerful confident smirk. IMPORTANT arms and palms clearly visible, hands not folded, not fists, not dab. No numbers or other props in this atlas: floating numerals 6 and 7 will be animated independently in the application, palms must be empty. Entire canvas background TRUE alpha TRANSPARENT, no ground, no shadow ellipse, no black or white backdrop, no checkerboard pattern drawn, no cell borders, no labels, no text, no watermark. Character should occupy central 78% of EACH square cell, including arms, with generous equal transparent margins. Keep horn, ears, hands, shoes all inside each cell, no overlap. Consistent face and outfit exactly, crisp game-ready sprites. Landscape canvas 3:2, ideally1536x1024 for six512x512 cells.

## Comprobación

Las pruebas de Windows verifican los seis recortes, alternancia de manos y números, activación y parada, restauración del traje, funcionamiento en alumnos, registro/desregistro del atajo y detección de combinaciones ocupadas.
