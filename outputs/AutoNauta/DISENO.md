# AutoNauta 2.2 — diseño de la interfaz

Rediseño visual de la ventana principal, el indicador flotante y el menú junto al reloj. Fecha: 13 de octubre de 2026. Las capturas de `diseno-2.2/` salen de `--ui-smoke` con datos de ejemplo; no contienen cuentas, contraseñas ni datos de sesión.

> **Estado de la entrega:** Codex revisó el trabajo de Claude y compiló `AutoNauta.exe` como **2.2.0.0**. La versión anterior se conserva localmente en `work/revision-base-2.1.1`. Verificación independiente y mediciones: `REVISION-DISENO-2.2.md`.

## Dirección visual

- **Tema grafito oscuro con un solo acento.** Fondo `#101317`, tarjetas `#191D23`, controles `#22272F`, bordes `#2D343E`. El azul claro `#8AB4F8` marca solo la acción principal, el foco y los estados activos (AUTO, chincheta, interruptor). Así no compite con los colores del LED.
- **Colores de estado sin cambio de significado:** verde `#3DD68C` conexión comprobada, amarillo `#F2C14E` espera o reconexión, rojo `#F4606A` error, gris `#8A94A1` pausa o espera.
- **Tipografía Segoe UI** (Semibold para titulares y cifras). Tres tamaños principales: titular de estado 18 px, cifras 17 px y texto 12 px. Las notas usan 11 px.
- **Contraste:** el texto secundario `#A6B0BD` alcanza unos 7,7:1 sobre las tarjetas; el terciario `#8893A1`, unos 5,4:1. El botón principal usa texto casi negro sobre azul claro, con una relación de unos 8,9:1. Solo el texto deshabilitado queda por debajo de 4,5:1, como corresponde.
- **Iconos dibujados con líneas** (reloj, flechas, chincheta y marca de verificación): no hay archivos de imagen y se ven nítidos con cualquier escala.
- **Sin animaciones permanentes.** Solo se mantiene la transición de apertura y cierre de la barra.

## Ventana principal

Orden de lectura: **estado → funcionamiento → cuenta → actividad**.

1. **Estado:** LED, titular (el mensaje del motor, a todo el ancho), «Desde las HH:mm:ss», detalle en dos líneas y tres cifras: tiempo restante, bajada y subida. La nota del tiempo explica su origen o por qué no está disponible. **Comprobar ahora** queda junto a esa nota.
2. **Funcionamiento**, dos filas separadas:
   - **Vigilancia:** botón **Iniciar** (acción principal en azul) o **Pausar** (secundario), con la descripción del estado.
   - **Login automático:** interruptor. La descripción dice «AUTO · …» o «MAN · …», igual que la barra.
   Pausar y desactivar el login automático siguen siendo acciones distintas y llaman a los mismos métodos del motor.
3. **Cuenta Nauta:** si no hay cuenta guardada, el formulario aparece abierto. Si la hay, se pliega en un resumen («ej•••@nauta.com.cu · protegida por Windows») con **Cambiar cuenta**. Al abrirlo aparecen **Guardar cuenta**, **Eliminar cuenta** y **Cancelar**. Intro guarda y Esc cancela. Los mensajes de validación aparecen bajo los campos en amarillo, incluidos los del motor (por ejemplo, «terminada en @nauta.com.cu»).
4. **Actividad reciente:** cada fila muestra el color del estado, la hora, el mensaje y el detalle. La información completa aparece en un tooltip.
5. **Pie:** «Cerrar la ventana no detiene AutoNauta.», **Minimizar** y **Salir**.

La X sigue ocultando la ventana; **Salir** sigue siendo la única salida explícita. La barra de título se oscurece mediante DWM en Windows 10 2004+ y 11; en versiones anteriores Windows ignora el atributo.

Cuando la pantalla es más baja que el contenido, la ventana se desplaza verticalmente y la lista conserva una altura mínima. Al abrir, la ventana ajusta su altura al contenido con unas cinco filas de actividad, dentro del área útil del monitor.

## Indicador flotante

- **Reposo:** cápsula ovalada de 46×30 px a 100 % (antes, 52×42), con el LED y un halo. No muestra texto.
- **Expandida (426 px):** reloj y tiempo, ↓ bajada, ↑ subida, pastilla **AUTO**/**MAN** y chincheta. Las columnas tienen ancho fijo para que las cifras no hagan saltar la barra. Sin tiempo muestra «No disponible» en gris.
- **Bordes suavizados:** la ventana es *layered* (`WS_EX_LAYERED` + `UpdateLayeredWindow`). Se dibuja en un único DIB premultiplicado del tamaño expandido, creado una vez y reutilizado en cada cuadro. Antes, una región GDI recortaba la ventana con bordes dentados.
- El contenido queda fijo respecto al LED mientras la cápsula se abre. Si no hay espacio a la derecha, la barra se abre hacia la izquierda y el LED queda bajo el cursor.
- **AUTO/MAN y chincheta** cambian de aspecto al pasar el mouse y muestran un tooltip propio con lo que hará el clic.
- **Se conservan:** estilos sin minimizar ni maximizar, `WS_EX_NOACTIVATE`/`WS_EX_TOOLWINDOW`, `SC_MINIMIZE` ignorado, restauración por evento y **Recuperar LED**.
- **Nuevo:** al restaurarse, WinForms intentaba aplicar su tamaño recordado. `SetBoundsCore` hace que esos límites sigan a la superficie dibujada, y un `WM_WINDOWPOSCHANGED` desfasado provoca un único redibujado. Todo funciona por evento, sin sondeos.

## Escalado de Windows

El proceso de interfaz se declara *DPI-aware* (`SetProcessDPIAware`) para que Windows no estire la ventana como un mapa de bits y el texto se vea nítido. Todas las medidas se escriben en unidades de 96 DPI y se multiplican por la escala del sistema; las fuentes usan píxeles con el mismo factor.

La posición del LED se sigue guardando en unidades de 96 DPI, como la registraban las versiones anteriores, que no reconocían DPI. Por eso una posición existente aparece en el mismo sitio al actualizar. Con varios monitores con escalas distintas, Windows escala por mapa de bits la ventana que esté en el monitor secundario, igual que antes.

Para revisar otras escalas sin cambiar Windows, la vista previa acepta `AUTONAUTA_UI_SCALE` (1–3). Solo se tiene en cuenta con `--preview` y `--ui-smoke`.

## Recursos y rendimiento

- Siguen igual: las actualizaciones agrupadas (`metricsPending`), la ausencia de trabajo de interfaz con solo el LED visible y la reutilización de los cuatro iconos de bandeja.
- El LED se redibuja solo si cambia algo visible: color, cifra mostrada, AUTO/MAN, fijación, elemento bajo el mouse o tamaño durante la transición. La prueba visual cuenta los redibujados.
- Las fuentes se crean una vez (`Theme`) y se liberan al cerrar. El DIB, el DC de memoria, `StringFormat`, tooltips y temporizadores se liberan en `Dispose`.
- No se añadieron temporizadores ni sondeos. Los únicos temporizadores siguen siendo los de la transición (15 ms, solo mientras se anima), la salida del mouse (180 ms, solo con la barra abierta y sin fijar) y el guardado diferido de la posición.
- La prueba visual repite 300 ciclos de expansión, cambio de cifras y estados tras una ronda de calentamiento. No aumentan los objetos GDI ni de usuario.

## Archivos

- `src/UiTheme.cs` (nuevo): paleta, escala, fuentes, botones, interruptor, campos, LED, lista de actividad, menú oscuro, iconos y DWM.
- `src/UiMain.cs` (nuevo): distribución de la ventana principal.
- `src/Ui.cs`: `UiContext` adaptado a la nueva ventana e `IndicatorForm` reescrito como ventana *layered*. La prueba visual se amplió.
- `src/Program.cs`: una línea, `Native.SetProcessDPIAware()`.
- `src/AssemblyInfo.cs`: versión 2.2.0.0.

No cambiaron `Engine`, `Network`, `PortalClient`, `CaptiveProbe`, `SocketPath`, `Storage`, `AccountTime` ni `RateMonitor`.

## Cambios de comportamiento solo de interfaz

- **Eliminar cuenta** pide confirmación (No por defecto). El motor ya pausaba al eliminar y el diálogo lo dice.
- **Eliminar cuenta** se deshabilita si no hay nada guardado.
- Los errores de validación de `SaveAccount` (`ArgumentException`) se muestran con su texto. Antes aparecía un mensaje genérico.
- `--preview` muestra cifras de ejemplo, avisadas en una franja amarilla, para revisar el diseño. Sigue sin usar la red.

## Pendientes y límites

- El ejecutable ya fue reemplazado y abierto con la cuenta existente. El usuario confirmó que el LED sigue visible tras «Mostrar escritorio», se despliega al pasar el mouse y aparece en la posición esperada. La comprobación está registrada en `REVISION-DISENO-2.2.md`; siguen pendientes Win+D, arrastre a cada esquina y monitores con escalas distintas.
- Con un tema claro de Windows la aplicación sigue oscura: no se implementó el cambio automático de tema.
- El interruptor de login automático no muestra texto propio; su estado se lee en la descripción (AUTO/MAN) y lo anuncian los lectores de pantalla como casilla.
- No se probó con monitores de escalas distintas ni con lector de pantalla.
