# AutoNauta 2.4.2 — prioridad del indicador

13 de octubre de 2026. El usuario observó que BF6 tapaba el indicador y confirmó que juega en «Pantalla completa».

`TopMost` ya estaba activo, pero no garantiza ser la primera entre varias ventanas superiores. Se añade `OverlayOrderGuard` al indicador: escucha `EVENT_SYSTEM_FOREGROUND` y eventos de aparición/reordenación de ventanas mediante `SetWinEventHook` con `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`. El delegado se conserva durante el registro y los eventos se eliminan al destruir o cerrar el indicador.

Los avisos se agrupan en una llamada pendiente de la interfaz. Se revisa el orden de ventanas y solo se eleva si otra ventana visible de un proceso distinto se superpone al indicador o si perdió el estilo superior. Las ventanas propias, incluidos menús y tooltips, quedan disponibles. El movimiento y los menús suspenden la elevación; al terminar se comprueba de nuevo. Se usa `SetWindowPos` con `SWP_NOACTIVATE`, sin mover, redimensionar ni repintar. Para una ventana ya superior se usa `HWND_TOP` y, si perdió ese estilo, `HWND_TOPMOST`.

Después del aviso hay una segunda comprobación a los 350 ms para cubrir cambios de orden tardíos durante el arranque de una ventana. Ese temporizador se detiene al ejecutarse; no hay sondeo permanente. Se conservan barra compacta, fijación, ocultamiento en verde, posición del LED, recuperación tras «Mostrar escritorio», reconexión, contador y estadísticas.

## Validación medida

- 92 pruebas de núcleo correctas, cero fallos. Compilación de candidato en carpeta temporal mientras la entrega anterior seguía activa.
- Pruebas de interfaz a 100 %, 150 % y 200 %: salida 0, 34 capturas por escala. Una comprobación final adicional cubrió la integración al volver de menú/arrastre y mostrar el indicador.
- Caso de otra ventana superior: recuperación de orden, foco intacto, agrupación de 100 avisos, posición y superficie sin cambios, comprobación diferida detenida y respeto de ventanas ocultas. Cierre con aviso pendiente libera los eventos y el temporizador.
- `tests/OverlayOrderProbe.cs`: dos procesos separados, eventos reales de Windows, ventana competidora superior y promoción posterior. El indicador recuperó el orden en ambas fases y el foco se conservó. Esta prueba utiliza ventanas WinForms; no reproduce la presentación gráfica de BF6.
- Instalación de 2.4.2.0 en `outputs/AutoNauta/AutoNauta.exe`, respaldo local de ejecutable/configuración anterior y coincidencia SHA256 entre candidato y entrega. Se conservaron cuenta, contraseña y sesión protegidas, modo, fijación y posición. Los respaldos personales quedan fuera de Git.
- Confirmación manual del usuario después de instalar: «Sí, ahora se ve» encima de BF6 en su configuración de pantalla completa. No fue necesario cambiar el modo del juego.
- Muestra local de recursos de 30,32 segundos: CPU 0,032 % del equipo (16 procesadores lógicos), memoria privada máxima 30,38 MiB, GDI 48→48 y objetos USER 75→77. Es una muestra corta del proceso activo; no demuestra estabilidad de recursos durante días ni mide el impacto en los FPS de BF6.

La prueba nativa puede reproducirse compilando `tests/OverlayOrderProbe.cs` junto a `src/OverlayOrderGuard.cs`, con el compilador .NET Framework x64, destino `exe` y referencias `System.Windows.Forms.dll` y `System.Drawing.dll`. Ejecutar ese binario abre dos ventanas de prueba brevemente y termina con código 0 si recupera el orden y conserva el foco. No consulta red ni datos de cuenta. La prueba de interfaz se ejecuta con `AutoNauta.exe --ui-smoke <carpeta>` y `AUTONAUTA_UI_SCALE` igual a `1`, `1.5` o `2`.

## Límites de compatibilidad

Esto refuerza el orden de ventanas de escritorio; no garantiza superponerse a todas las aplicaciones ni a una presentación exclusiva, escritorios protegidos o a una aplicación que imponga continuamente su propio orden. No se modifican BF6, su configuración ni las opciones de Windows. La confirmación del usuario en BF6 valida su configuración actual. Si en otro juego la presentación de pantalla completa impide mostrar ventanas de escritorio, usar ventana sin bordes es la alternativa compatible con este indicador. «Pantalla completa» en el menú de un juego no demuestra por sí sola que use presentación exclusiva.

Fuentes de implementación: [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos), [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook), [constantes de eventos](https://learn.microsoft.com/en-us/windows/win32/winauto/event-constants) y [presentación DXGI y ventana sin bordes](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model).

Actualizar reinicia los contadores de esta ejecución; se conservan registro histórico y contexto protegido del contador de horas.

Paquete local `outputs/releases/v2.4.2/AutoNauta-v2.4.2-windows-x64.zip`, 105846 bytes, SHA256 `04ef4def14b5b56478dd69af85b482cf5a7a0893bc61ac2ec195268303c9bdd5`. Contiene ejecutable, guía y sumas SHA256; no incluye configuración personal. Prepararlo no publica una Release en GitHub.
