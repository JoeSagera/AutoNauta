# LED lateral discreto — diseño de Claude integrado en 2.3.0

13 de octubre de 2026. Encargo: `docs/PROMPT-CLAUDE-LED-LATERAL.md`. Solo cambia la interfaz del indicador (`IndicatorForm` y `UiContext` en `src/Ui.cs`). No se tocaron la reconexión, el contador de 2.2.2, las credenciales ni el motor. Las capturas de `diseno-lateral/` salen de `--ui-smoke` con datos ficticios.

## Comportamiento

| Situación | Qué hace el indicador |
|---|---|
| Verde, sin interacción ni fijación | Tras **500 ms** se desplaza al **lateral más cercano** del área útil de su monitor, a la misma altura, y queda como muesca. |
| Mouse sobre la muesca | Revela la barra (tiempo, ↓, ↑, AUTO/MAN, chincheta) hacia el interior, en unos 180 ms. |
| Mouse fuera de la barra | Espera **500 ms** y vuelve a la muesca. Si el mouse vuelve durante el ocultamiento, la transición se invierte sin cerrarse. |
| Amarillo o rojo | Cancela cualquier ocultamiento o transición y muestra **en el acto** la cápsula en la posición habitual del usuario. La barra se abre con el mouse, como antes. |
| Gris | Cápsula completa visible en su posición; nunca se oculta. |
| Vuelta al verde | Programa de nuevo el ocultamiento, respetando mouse, menú, arrastre y fijación. |
| Barra fijada | Visible y expandida en cualquier estado. En verde permanece en el lateral; en amarillo o rojo, en la posición habitual. Al quitar la fijación vuelve al modo automático. |
| Menú contextual abierto | Suspende el ocultamiento; lo reanuda al cerrarse. |
| Arrastre | Suspende el ocultamiento. Arranca desde donde se ve la cápsula, sin saltos. Al soltar se guarda la nueva posición y el lateral se recalcula a partir de ella. |
| «Recuperar LED» | Muestra la cápsula en el lateral durante **2,5 s**, sin abrir la ventana ni tomar el foco, y vuelve a ocultarse una sola vez. |
| «Mostrar escritorio» | La recuperación por evento conserva el estado (muesca o cápsula) sin revelar ni crear bucles. |

Lo decide el **color real** de la conexión, no el modo AUTO/MAN.

La posición guardada (`IndicatorX/Y`) es siempre el ancla elegida por el usuario. La geometría de la muesca se deriva de ella y no se guarda en ningún momento, ni siquiera durante las animaciones.

## Medidas finales (a 100 %, escalan con Windows)

| Elemento | 2.2 | Ahora |
|---|---|---|
| Cápsula replegada | 46×30 px | **32×26 px**: LED de 10 px con halo de 16 px y unos 5 px de margen vertical |
| Barra expandida | 426×30 px | **400×26 px**: columnas de 108/88/88 px, AUTO/MAN de 46×18 y chincheta de 22×22 |
| Muesca | — | **7 px de profundidad × 22 px de alto** (ventana de 7×26 con bordes transparentes) |

La muesca tiene forma de «D»: plana contra el borde, con esquinas redondeadas hacia la pantalla y una gota vertical del color de estado (2,5×12 px). La transición dura 180 ms; el recorrido largo desde la posición del usuario hasta el borde, 220 ms. No hay pulsaciones ni brillos animados.

Altura constante: la muesca, la cápsula y la barra comparten la misma franja vertical. Así la zona bajo el cursor es continua y no se producen ciclos de abrir y cerrar.

## Implementación

- Geometría animada `{X, ancho, grado de muesca}` con interpolación por tiempo (Stopwatch) y suavizado cúbico. El dibujo hace una transición continua entre cápsula y muesca.
- Hay dos temporizadores y solo funcionan mientras hacen falta: la animación (15 ms) durante la transición y el retardo (500, 180 o 2500 ms) mientras hay algo pendiente. Con la muesca estable, los dos quedan detenidos. El mouse, el menú (`Opening`/`Closed`), el arrastre y la fijación vuelven a programar el retardo; no se sondea nada.
- Lateral: `NearestRight` y `DockedX` trabajan sobre el área útil del monitor que contiene el ancla. La muesca respeta una barra de tareas lateral y queda dentro de su monitor, también con coordenadas negativas. Los cambios de resolución o de monitores vuelven a calcularla mediante el evento ya existente `DisplaySettingsChanged`.
- Con la muesca no se actualizan ni dibujan métricas, porque `Expanded` es falso. Los estados repetidos tampoco redibujan.
- `UpdateLayeredWindow` no siempre actualiza la caché de límites de WinForms; tras cada dibujo se sincroniza con `UpdateBounds()` sin mover la ventana.
- Al liberar se cancelan las suscripciones al menú y se liberan los temporizadores, el DIB y el DC (como antes).
- **Solo para la vista previa**, `AUTONAUTA_PREVIEW_STATE=verde` aplica un estado verde de ejemplo para probar la muesca con el mouse real. Fuera de `--preview` no tiene efecto.

## Validación

- **Pruebas funcionales:** 76 correctas, 0 fallidas (en `Compilar.ps1` y en la compilación temporal).
- **`Compilar.ps1`:** generó `outputs/AutoNauta/AutoNauta.exe`. La instancia personal (`outputs/releases/v2.2.1/…`, PID sin cambios) siguió en ejecución, sin cerrarla ni reemplazarla. El `--ui-smoke` del ejecutable oficial terminó con salida 0 y 26 capturas.
- **`--ui-smoke`:** salida 0 a 100 %, dos veces a 150 % (escala real) y a 200 %, con 26 capturas en cada una. Se mantienen todas las comprobaciones anteriores del LED y la ventana. Comprobaciones nuevas:
  - cápsula de 34×28 px como máximo, ovalada;
  - geometría pura con monitor de coordenadas negativas y barra de tareas lateral;
  - en ambos laterales: gris visible, verde con retardo de 400–600 ms, ocultamiento animado dentro del monitor, muesca pegada al borde y a la altura elegida, sin temporizadores activos, sin redibujados por métricas invisibles ni por estados repetidos, apertura que cubre la muesca (zona continua), barra hacia el interior, retardo al salir e inversión si el mouse vuelve;
  - amarillo a mitad de la revelación y rojo a mitad del viaje al borde: cápsula inmediata en la posición del usuario; gris visible;
  - fijar en verde, pasar a amarillo y volver a verde, y desfijar;
  - menú abierto que suspende y luego reanuda;
  - arrastre: suspende, conserva la posición y recalcula el lateral;
  - «Recuperar LED» temporal, sin abrir la ventana ni cambiar el foco y sin bucle;
  - minimización forzada que conserva la muesca;
  - 150 ciclos de ocultar, revelar y cambiar de estado sin crecimiento de objetos GDI ni de usuario tras el calentamiento.
- **Mouse real sobre `--preview` (150 %):** la muesca de 10×39 px quedó en el borde derecho. Al acercar el cursor, la barra se abrió a 600×39 px en menos de unos 160 ms, sin cambiar la ventana activa. Siguió abierta al recorrerla y 300 ms después de salir, y había vuelto a la muesca al segundo. Las capturas de esta prueba mostraban el escritorio y no se guardaron.
- **Consumo observado:** con la vista previa en reposo, la muesca y la ventana visible, la interfaz usó **0 ms de CPU en 30 s**; GDI pasó de 65 a 65 y USER de 68 a 67, con unos 24 MiB de memoria privada. La vista previa no ejecuta el motor; en la aplicación real hay que sumar el muestreo y la vigilancia de siempre. No se repitió la medición comparativa con `work/PerformanceHarness.cs`.

## Pendiente de comprobar a mano

- **«Mostrar escritorio» y Win+D reales** con la muesca: la prueba reproduce la minimización forzada, no el gesto del shell.
- **Juegos:** se espera que sea visible sobre juegos en ventana sin bordes, porque es una ventana TopMost no activable. **No se verificó**, y no se promete, sobre pantalla completa exclusiva (DirectX/Vulkan), donde Windows suele no componer otras ventanas.
- **Varios monitores reales:** borde compartido entre monitores (la muesca sigue siendo alcanzable, pero no es un borde «infinito» para el mouse), escalas distintas por monitor (el proceso solo reconoce la escala del sistema) y desconexión de un monitor con la muesca encima.
- **Barra de tareas lateral real:** cubierto con geometría sintética, no con una barra real.
- **Uso prolongado** con la red real y medición comparativa con `work/Measure-Process.ps1`.

## Límites y decisiones para Codex

- En el borde derecho, la muesca (7 px) puede tapar parte de la barra de desplazamiento de una aplicación maximizada a esa altura. Arrastrándola se cambia la altura o el lado.
- No hay opción para desactivar el ocultamiento lateral: siempre está activo en verde. Si hace falta, puede añadirse como preferencia sin tocar la lógica.
- Codex integra esta entrega como **2.3.0.0**. La validación independiente y la instalación se documentan en `REVISION-LED-LATERAL-2.3.md`; publicar una Release de 2.3.0 es un paso separado.
- La posición guardada de la cápsula anterior (46×30) se reutiliza como esquina superior izquierda de la nueva (32×26). El LED queda unos píxeles más arriba y a la izquierda.
