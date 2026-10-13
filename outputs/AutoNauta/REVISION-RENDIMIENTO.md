# AutoNauta 2.1 — revisión de recursos y funcionamiento

Se revisaron motor, interfaz, red, contador de cuenta, cifrado, escritura de configuración y registros. La aplicación se recompiló y quedó funcionando minimizada, con el LED visible. Se conserva la cuenta, la posición y el modo de login.

## Resultado medido

Comparación en este equipo de 16 procesadores lógicos, con instancias de prueba equivalentes, sin credenciales, red real y velocidades activas. Cada medición dura aproximadamente dos minutos, después de 20 segundos de calentamiento. El proceso de prueba también toma sus propias muestras; los porcentajes incluyen ese pequeño coste, presente en ambas versiones.

| Escenario | CPU total anterior | CPU total optimizada | Reducción relativa |
|---|---:|---:|---:|
| Ventana minimizada, LED | 0,071 % | 0,042 % | 40 % |
| Ventana minimizada, barra expandida | 0,076 % | 0,051 % | 33 % |

La medición inicial del programa real, ya calentado, fue de 0,031 % de CPU total durante un minuto, con un pico de 66 MiB residentes. No debe compararse directamente con la tabla: el escenario y el método de muestreo son distintos.

El ejecutable entregado, con la cuenta conservada y ejecutándose minimizado, consumió **0,082 % de CPU total** durante una medición de 60,55 segundos. El pico de memoria residente fue **55,34 MiB** y el de memoria privada **31,05 MiB**. Esta muestra se tomó durante sus primeros minutos de ejecución; no demuestra una reducción frente a la muestra inicial del programa real. Las reducciones de la tabla proceden únicamente de los escenarios de prueba equivalentes. En este equipo, 0,082 % total equivale a 1,316 % de un solo procesador lógico.

En una ejecución de prueba de cinco minutos de la versión optimizada, el uso medio fue 0,043 % de CPU total. La memoria privada se estabilizó alrededor de 36 MiB: en los dos últimos minutos osciló entre 35,71 y 35,95 MiB. La memoria residente terminó alrededor de 60 MiB. Los recursos gráficos GDI permanecieron en 50; los recursos de usuario se estabilizaron en 66. Los handles fluctuaron y descendieron durante las recolecciones normales; no crecieron continuamente en esa prueba.

La memoria residente incluye bibliotecas de .NET y Windows Forms; la memoria privada y la residente son métricas diferentes. No se promete consumo cero ni se interpreta una prueba de cinco minutos como validación de semanas de ejecución.

## Hallazgos y cambios

| Hallazgo | Cambio y efecto |
|---|---|
| Cada muestra de velocidad actualizaba el LED y controles de la ventana oculta. | Con solo el LED visible no se encolan actualizaciones de velocidades para la interfaz ni se redibuja por esos datos. Al abrir la barra o la ventana se obtiene inmediatamente la muestra actual. |
| Datos idénticos provocaban repintados y actualizaciones de tooltip. | Se comparan las cadenas que se muestran y se redibuja únicamente si cambian. Las actualizaciones pendientes de métricas se agrupan en una sola. |
| El icono se recreaba por cada cambio de estado. | Se reutiliza un máximo de cuatro iconos, uno por color, y se liberan al cerrar. Las regiones y objetos de dibujo temporales también se liberan. |
| Se enumeraban todos los adaptadores cada segundo. | Se conserva el inventario de interfaces físicas y se siguen leyendo sus contadores cada segundo. El inventario se renueva al cambiar la red y cada 30 segundos como respaldo. Las suscripciones se retiran al cerrar. |
| Se repetía DNS en cada comprobación sana. | Cache de 120 segundos; se invalida ante un fallo, una ruta incompatible o un cambio de red. Cada comprobación sigue validando HTTPS y la salida por VPN. |
| Se calculaba información detallada del certificado en cada conexión válida; el objeto de hash no se liberaba explícitamente. | La información detallada se obtiene ante errores y se liberan certificado copiado y hash. Se conserva la validación de nombre, fechas y confianza. |
| Algunas operaciones asíncronas creaban eventos de espera innecesarios. | DNS y conexión TCP consumen su finalización mediante tareas; las tareas se liberan y se observan también sus errores si la espera se cancela. |
| Los timeouts por lectura podían renovarse con una respuesta lenta indefinidamente. | Hay presupuesto total: cinco segundos en sondas HTTPS, ocho por petición Nauta, seis en detección cautiva y quince para una cadena de redirecciones del portal. El socket se cierra al agotar el plazo o cancelar. La resolución inicial de DNS mantiene su límite de tres segundos. |
| Un timeout durante TLS podía marcar la cuenta como bloqueada por certificado. | Se distingue el agotamiento del plazo de un certificado inválido. No se omite la validación HTTPS. |
| Sin contador de sesión, se repetía una consulta informativa que solo devolvía crédito e historial. | Esa respuesta se conserva sin consultas automáticas repetidas. Un nuevo login, cambio de cuenta o comprobación manual permite volver a consultar. Con un contador válido, el tiempo sigue actualizándose aproximadamente cada minuto. |
| Un error temporal del contador podía borrar una sesión todavía válida. | Se conserva el identificador ante respuestas temporales o desconocidas. Solo se invalida ante una señal definida de sesión terminada, cambio de cuenta o acceso cautivo confirmado. |
| Comprobar manualmente podía esperar por el bloqueo del ciclo de red. | La petición se señaliza de inmediato; la interfaz no espera a que termine una llamada de red. |
| El cierre podía ejecutarse dos veces y dejaba abierto el evento del trabajador. | Cierre idempotente, retirada de temporizadores y suscripciones, liberación del evento al terminar el trabajador y guardado final sin duplicación. |
| La configuración idéntica se volvía a escribir; los archivos de log podían acumularse. | Se evitan escrituras idénticas y se serializan las operaciones de archivo. Los eventos tienen un archivo activo de unos 512 KiB y cuatro archivos rotados; solo se gestionan nombres propios del programa. |

La vigilancia conserva su intervalo configurado: por defecto 15 segundos, con confirmación de dos fallos antes de actuar. Las velocidades siguen muestreándose cada segundo, incluso si la interfaz está oculta. Se conservan login AUTO/MAN, pausa, fijación, arrastre y consulta de tiempo. No se cambiaron WireGuard, rutas ni filtros del sistema.

## Verificación

- **60 pruebas correctas, cero fallos.** Las nuevas pruebas cubren cache DNS, caducidad e invalidación concurrente, cancelación, clasificación de timeout TLS, configuración sin escrituras repetidas, retención de logs, cierre idempotente y liberación del evento, comprobación manual inmediata y preservación del contador ante errores temporales.
- Un servidor local de prueba acepta TCP y no responde al handshake TLS: la petición termina por su plazo total. No se desactivó HTTPS para probarlo.
- Prueba visual correcta: 1.000 notificaciones de métricas en modo LED no encolan trabajo de interfaz; las velocidades ocultas no provocan repintados; las visibles sí; datos idénticos no se repintan. Se comprueba la reutilización de iconos, los cuatro colores, expansión, fijación y controles.
- Diagnóstico del ejecutable entregado, sin administrador: portal HTTP 200 y formulario reconocido por Ethernet, con Internet por WireGuard antes y después.
- Las instancias de comparación no usaron la cuenta del usuario ni iniciaron sesiones Nauta. La aplicación real se reinició con su configuración conservada.

## Límites pendientes

Una caída real de Nauta y su contador requieren una sesión nueva; no se desconectó deliberadamente la sesión actual. La prueba de estabilidad dura cinco minutos y no acredita ausencia de problemas en todas las redes, equipos o durante semanas. En TLS, la verificación de certificados de Windows puede implicar trabajo interno del sistema; cerrar el socket limita la espera de red del programa, pero no cancela por la fuerza funciones internas de Windows.

## Evidencias reproducibles

En el proyecto se conservan `work/Measure-Process.ps1`, `work/PerformanceHarness.cs`, la copia anterior `work/backup-v2` y las muestras JSON de las comparaciones. La carpeta `evidencias-rendimiento` de esta entrega contiene los resultados medidos. Las cifras pueden variar con el tráfico y la carga del equipo.

Referencias técnicas: [notificaciones de cambios de red de Microsoft](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.networkchange?view=netframework-4.8.1), [liberación de temporizadores](https://learn.microsoft.com/en-us/dotnet/api/system.threading.timer.dispose?view=netframework-4.8.1).
