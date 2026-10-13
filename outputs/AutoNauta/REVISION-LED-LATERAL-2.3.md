# AutoNauta 2.3 — revisión e integración de Codex

13 de octubre de 2026. Claude es el diseñador principal. Codex revisó su entrega, la compiló desde las fuentes e instaló la versión **2.3.0.0**. Se verificó que cuenta, contraseña y sesión protegidas, modo AUTO y fijación siguen iguales. Se reutilizó la posición guardada al iniciar; el indicador sigue siendo arrastrable y su posición cambió durante la comprobación manual. La copia anterior y los archivos personales de respaldo permanecen únicamente en `work/`, fuera del repositorio.

## Resultado

La cápsula pasa de 46×30 a 32×26 px a escala 100 %. Con conexión verde se repliega después de 500 ms hasta una muesca de 7×22 px junto al lateral más cercano del monitor. El mouse revela la barra hacia el interior; al salir vuelve a esconderse. En amarillo, rojo o gris aparece el indicador completo en la posición habitual. La fijación, el menú y el arrastre suspenden el ocultamiento.

Se revisaron las transiciones interrumpidas, el ancla guardada separada de la geometría temporal, la zona continua bajo el cursor, las ventanas no activables, la recuperación del shell y la liberación de recursos. No se encontraron defectos bloqueantes en el cambio. El motor, la red, la autenticación, el contador y las credenciales mantienen el código de 2.2.2.

## Validación independiente

- `Compilar.ps1`: **76 pruebas correctas, cero fallos**, incluidas reconexión sin popup y contador de Nauta Hogar.
- `--ui-smoke`: **salida 0 y 26 capturas** a 100 %, 150 % y 200 %, en carpetas separadas. La compilación final 2.3.0 pasó de nuevo a 150 %.
- Se revisó visualmente la barra expandida: tiempo, velocidades, AUTO/MAN y chincheta permanecen legibles. Las capturas utilizan datos ficticios.
- Las comprobaciones incluyen ambos bordes, verde→amarillo/rojo durante una transición, gris, fijación, menú, arrastre, recuperación temporal y minimización forzada. Los ciclos de ocultar/revelar no acumularon objetos GDI ni de usuario; una muesca estable no mantiene temporizadores de transición/retardo ni redibuja por métricas invisibles.
- Un primer intento independiente falló en la aserción conjunta de visibilidad/foco de «Recuperar LED». El mensaje no identificaba qué parte falló. Se conservó el error local; las tres ejecuciones posteriores completas y la final 2.3.0 pasaron sin omitir esa comprobación. No se atribuye el fallo inicial a una causa demostrada.
- El ejecutable instalado coincide por SHA256 con el compilado y se comprobó que el proceso nuevo responde. Windows mantuvo brevemente bloqueado el archivo tras detener la instancia anterior; la sustitución se completó en el siguiente intento.

**Confirmación manual del usuario con 2.3 instalada:** preguntado por ocultamiento en verde, revelación con el contador y acceso tras pulsar «Mostrar escritorio», respondió: **«Sí, todo funciona bien»**.

## Consumo medido

Dos instancias de prueba independientes, sin credenciales y con login automático desactivado, usaron el mismo instrumento: 20 s de calentamiento y aproximadamente 60 s de medición simultánea, con vigilancia real y muestreo de tráfico. El monitor usa escala del sistema de 150 % y el equipo dispone de 16 procesadores lógicos. La versión nueva terminó con Internet comprobado y muesca oculta de 10 px; la anterior terminó con cápsula de 69 px. Los datos completos están en `evidencias-led-lateral-2.3/`.

| Medida | Base 2.2.2 | LED lateral |
|---|---:|---:|
| Duración medida | 60,07 s | 60,11 s |
| Tiempo total de CPU | 0,359 s | 0,500 s |
| CPU sobre todo el equipo | 0,037 % | 0,052 % |
| Memoria residente final | 57,46 MiB | 57,26 MiB |
| Memoria privada final | 32,60 MiB | 32,72 MiB |
| Objetos GDI inicial/final | 51 / 51 | 51 / 51 |
| Objetos de usuario inicial/final | 71 / 71 | 71 / 71 |

Esta muestra breve incluye motor y muestreo y muestra consumo bajo y memoria comparable. No demuestra una mejora universal de CPU ni estabilidad durante días. El crecimiento de memoria durante la muestra fue parecido en ambas instancias; no se interpreta por sí solo como una fuga.

## Pendientes

Continúan pendientes Win+D real, juegos en pantalla completa exclusiva, varios monitores reales con escalas distintas, barra de tareas lateral real, uso prolongado y una nueva caída real de Nauta. Esta revisión no provocó un logout, no repitió el login de la cuenta ni desconectó la VPN. No se anuncia una Release 2.3 publicada: la Release descargable previamente verificada sigue siendo 2.2.2.

Se preparó el paquete local `outputs/releases/v2.3.0/AutoNauta-v2.3.0-windows-x64.zip` (91179 bytes), con solo ejecutable, guía y sumas de comprobación. SHA256: `d396c15b891db2612120f0e141a96c91e151eba5cd60859307f39d52f4c9059a`. El paquete permanece fuera del historial Git hasta su publicación como asset de Release.
