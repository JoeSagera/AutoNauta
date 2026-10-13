# AutoNauta 2.2 — revisión e integración de Codex

13 de octubre de 2026. Claude es el diseñador principal. Se conservaron su dirección visual, distribución, paleta, controles y cápsula; Codex revisó el código, verificó la entrega y midió el consumo antes de integrarla.

## Alcance y resultado

Los cambios funcionales de interfaz están en `Ui.cs`, `UiMain.cs` y `UiTheme.cs`. `Program.cs` activa el reconocimiento de DPI antes de crear las ventanas; `AssemblyInfo.cs` identifica la versión 2.2.0.0. No cambiaron el motor, las reglas de autenticación, las sondas de red, el cifrado, el contador ni el cálculo de velocidades.

No se encontraron regresiones bloqueantes en la revisión del diseño. Se comprobó que las acciones siguen llamando a los métodos existentes del controlador, que vigilancia y login automático siguen separados y que el formulario preserva la contraseña guardada cuando se deja vacía. Las capturas son de vista previa con valores de ejemplo.

La entrega se compiló con `Compilar.ps1` después de detener la instancia anterior. Se abrió la nueva versión con `--resume`, con la cuenta y preferencias existentes. Se conserva una copia de la base 2.1.1 y del trabajo original de Claude en `work/`, solo local.

## Validación independiente

- **60 pruebas correctas, cero fallos**, tanto en la primera revisión como en la compilación final. El primer intento no pudo reemplazar el ejecutable porque la versión anterior estaba abierta; la compilación final completó la sustitución correctamente.
- Revisión visual con `--ui-smoke` a **100 %, 150 % y 200 %**, salida 0 y 17 capturas en cada escala, en carpetas nuevas por ejecución.
- Se revisaron las capturas de cuenta guardada, error, barra expandida y ventana pequeña. Las funciones permanecen accesibles mediante desplazamiento cuando la ventana no tiene altura suficiente.
- Se mantienen las comprobaciones del LED: estilos sin minimizar/maximizar, orden de minimizar ignorada, 20 minimizaciones forzadas, restauración sin tomar el foco, posición, apertura hacia ambos lados y barra fijada.
- Las métricas ocultas no provocan redibujados; valores visibles idénticos tampoco. La prueba de 300 ciclos, después de calentamiento, verifica que no se acumulan objetos gráficos o de usuario.
- Se revisó la liberación de fuentes compartidas, bitmap, DC de memoria, formatos de texto, iconos, tooltips y temporizadores.

## Recursos medidos

Comparación de la base 2.1.1 y la 2.2 con el mismo instrumento, sin credenciales, sondas públicas reales y muestreo de velocidades activo. Cada instancia tuvo 20 segundos de calentamiento y aproximadamente 120 segundos de medición. Los cuatro escenarios se ejecutaron simultáneamente en este equipo de 16 procesadores lógicos. El muestreo interno está incluido en el consumo.

Se ajustó `work/PerformanceHarness.cs` para activar DPI en las versiones que lo implementan, igual que hace la aplicación real. Las ventanas de prueba pueden solaparse; estas medidas son una comparación práctica corta, no una estimación de consumo máximo ni una garantía de funcionamiento durante semanas.

| Escenario | CPU total 2.1.1 | CPU total 2.2 | Memoria residente final 2.1.1 / 2.2 | Memoria privada final 2.1.1 / 2.2 |
|---|---:|---:|---:|---:|
| Minimizada, solo LED | 0,119 % | 0,113 % | 60,04 / 60,32 MiB | 35,90 / 36,03 MiB |
| Minimizada, barra fijada | 0,110 % | 0,079 % | 59,57 / 59,59 MiB | 35,56 / 35,45 MiB |

La nueva interfaz mantuvo un consumo bajo y memoria comparable en esta prueba. Los objetos GDI de la 2.2 permanecieron en 51, frente a 50 de la 2.1.1; los objetos de usuario pasaron de 71 a 72 en ambos escenarios nuevos. No se observó crecimiento continuo en el intervalo medido. Ambos escenarios de la 2.2 terminaron con Internet verificado y velocidades disponibles.

No se interpreta la diferencia porcentual entre estas muestras cortas como una mejora universal: carga del equipo, red y compositor pueden variar. No se añadieron temporizadores ni sondeos al programa de producción; el único ajuste de Codex fue al instrumento de comparación.

Las cuatro muestras originales están en `evidencias-rendimiento-2.2/`. Las cifras anteriores de `REVISION-RENDIMIENTO.md` corresponden a la versión 2.1.

## Comprobación manual y límites

La revisión automática reproduce minimización y recuperación, pero no ejecuta el botón real «Mostrar escritorio». Se solicitó al usuario comprobar el indicador nuevo, su expansión y su posición; el resultado se añadirá aquí cuando responda.

Siguen pendientes una caída real de Nauta con una sesión nueva y su contador, Win+D, monitores con escalas distintas, lector de pantalla y observación durante días. Esta revisión no desconectó la VPN ni provocó un nuevo login Nauta.

Referencias técnicas consultadas: [UpdateLayeredWindow de Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow), [DPI del sistema](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setprocessdpiaware). El proceso reconoce la escala del sistema; no se afirma soporte por monitor independiente.
