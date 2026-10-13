# Encargo para Claude: LED lateral discreto

Eres el diseñador principal de AutoNauta. Implementa esta mejora sobre la versión actual 2.2.2 en `D:\Proyectos\nauta_autologer`, conservando tu diseño grafito, los colores de estado y el bajo consumo en segundo plano.

## Comportamiento solicitado

- Con conexión comprobada y LED verde, sin interacción y sin fijación, el indicador se adhiere al borde lateral izquierdo o derecho más cercano del monitor donde está situado. Conserva su altura vertical dentro del área útil. En reposo queda una pequeña gota o muesca verde accesible con el mouse; no debe desaparecer por completo.
- Al pasar el mouse por la muesca, revela suavemente la barra con tiempo restante, bajada, subida, AUTO/MAN y chincheta. Abre hacia el interior del monitor y mantiene una zona de interacción continua entre muesca y barra, sin saltos ni ciclos de abrir/cerrar bajo el cursor.
- Al retirar el mouse, espera unos 400–600 ms y vuelve a la muesca si continúa verde. No ocultes durante arrastre, menú contextual, interacción ni barra fijada.
- Si cambia a amarillo o rojo, cancela el ocultamiento y cualquier transición incompatible: muestra inmediatamente el LED completo en cápsula compacta, como en el comportamiento actual. La barra informativa se abre al pasar el mouse o permanece abierta si está fijada; no abras la ventana principal ni tomes el foco.
- En gris (pausa/comprobación), mantén el LED completo visible. Oculta al borde únicamente cuando el estado sea verde.
- Al recuperar el verde, vuelve a ocultar después del breve retardo, respetando hover, arrastre, menú y fijación. El modo AUTO/MAN no decide el ocultamiento: lo decide el color real de la conexión.
- «Mantener barra expandida» tiene prioridad: mientras esté fijada, permanece visible y expandida también en verde. Al quitar la fijación y salir el mouse, vuelve al comportamiento automático.
- Conserva el arrastre y la posición elegida. Calcula el lateral más cercano después de mover el indicador; conserva el ancla preferida del usuario separada de la geometría temporal de ocultamiento. No sobrescribas la posición guardada por cada animación. En amarillo/rojo recupera la posición habitual de la cápsula; en verde deriva de esa posición el lateral de reposo.

## Dirección visual

Reduce el espacio vacío de la cápsula replegada, hoy de 46×30 px a escala 100 %. Propón aproximadamente 30–34×26–28 px, con LED legible y margen mínimo. Para la muesca, toma como referencia 5–8 px visibles de profundidad y 20–26 px de altura, con forma orgánica y bordes suaves; evita una franja larga o rectangular. Ajusta las cifras si hace falta para mejorar la facilidad de uso y documenta las medidas finales.

Mantén el acabado ovalado, el suavizado por píxel y la paleta actual. Sin texto en reposo, pulsaciones permanentes, brillos animados ni sonidos. La transición debe ser breve y suave (aproximadamente 150–220 ms). Mantén legibles las cifras y accesibles AUTO/MAN y la chincheta.

## Archivos y límites

El indicador vive en `src/Ui.cs`, clase `IndicatorForm`. Ahí están `SetStatus`, `SetExpanded`, `MoveAnchor`, `ResizeBar`, la recuperación de «Mostrar escritorio» y `UiContext.RunVisualSmoke`. Escala/paleta: `src/UiTheme.cs`; preferencias: `src/Models.cs`. Contexto del diseño: `outputs/AutoNauta/DISENO.md` y `outputs/AutoNauta/REVISION-DISENO-2.2.md`.

Esta entrega es de interfaz. Conserva la lógica de reconexión y el contador corregidos en 2.2.2, las credenciales protegidas y todas las acciones existentes. No agregues dependencias, procesos auxiliares, hooks globales del mouse ni sondeos continuos para detectar hover. Reutiliza eventos y temporizadores breves; los de animación/retardo deben detenerse al quedar estable. No actualices ni redibujes métricas invisibles. Libera recursos GDI, superficies y temporizadores correctamente.

Respeta ventanas no activables, transparencia, TopMost y recuperación por eventos de «Mostrar escritorio». «Recuperar LED» debe revelar temporalmente el indicador incluso estando verde, sin abrir la ventana principal ni tomar el foco. Evita que esa recuperación luche con el ocultamiento o genere un bucle de restauración.

Usa el área útil del monitor correspondiente, contempla coordenadas negativas, bordes entre monitores, barra de tareas lateral, cambios de resolución y desconexión de monitores. La muesca debe pertenecer a su monitor y poder alcanzarse; no debe terminar invisible en otro. Respeta el escalado que ya soporta el proyecto y documenta cualquier límite real. No prometas superposición sobre juegos con pantalla completa exclusiva sin verificarla.

## Validación y entrega

Amplía `--ui-smoke` con verde oculto y revelado en ambos lados; transiciones verde→amarillo/rojo durante ocultamiento y animación; vuelta a verde; gris; fijar/desfijar; menú; arrastre; «Recuperar LED»; y ausencia de redibujados innecesarios o crecimiento GDI. Adapta las aserciones que actualmente exigen 46×30 o una cápsula más ancha que alta; conserva su propósito. Mantén las 76 pruebas funcionales existentes.

Compila mediante `Compilar.ps1` sin cerrar ni reemplazar la instancia personal activa para hacer pruebas. Utiliza `--preview`/`--ui-smoke` con datos ficticios y capturas a 100 %, 150 % y 200 %. Verifica que no robe el foco y deja pendientes explícitos para las comprobaciones manuales de juegos, varios monitores y «Mostrar escritorio» que no puedas reproducir.

Entrega el código, capturas y una nota local con comportamiento, medidas finales, validación, consumo observado y límites pendientes. No incluyas datos privados ni páginas reales de Nauta. Deja la instalación y la publicación para la revisión e integración de Codex.
