# AutoNauta

Programa Windows para recuperar la sesión Nauta Hogar automáticamente, con WireGuard activo, LED ovalado desplegable, contador de tiempo y tráfico del sistema. La cuenta cifrada existente se conserva.

## Ejecutar y desarrollar

- Programa: `outputs/AutoNauta/AutoNauta.exe`.
- Guía: `outputs/AutoNauta/LEEME.md`.
- Validación: `outputs/AutoNauta/VALIDACION.md`.
- Fuentes C# / Windows Forms: `src/`.
- Ejecutar `Compilar.ps1` con la aplicación cerrada para compilar y correr las pruebas con .NET Framework x64, sin descargar dependencias.
- Cerrar la aplicación y ejecutar `Diagnosticar.ps1` para probar la red sin enviar credenciales.

## Decisión actual — 12 de octubre de 2026

El usuario cambió a WireGuard con servidores gratuitos. Se eliminó la solución específica para el bloqueo de la aplicación Proton: filtros WFP, modificación de rutas, elevación y proceso auxiliar de recuperación. El programa no configura ni activa/desactiva túneles VPN.

La carpeta nueva estaba vacía. Se trasladaron los fuentes útiles de la entrega anterior; la entrega original se conservó fuera de este proyecto. Los datos de cuenta siguen en su carpeta de Windows y no se copiaron al proyecto.

## Conexiones y decisiones

`SocketPath.Select` elige la interfaz de salida de cada socket IPv4 mediante IP_UNICAST_IF; solo afecta a ese socket. DNS del router, sonda HTTP cautiva y HTTPS Nauta usan la interfaz física. Las sondas HTTPS de Internet usan la interfaz WireGuard detectada y comprueban también su ruta. El verde requiere una respuesta exacta por VPN.

Tras dos fallos, una petición HTTP sin credenciales distingue Internet directo de una redirección al portal. Con Internet directo, espera a la VPN. Una respuesta ambigua espera sin autenticar. Solo una redirección al origen Nauta HTTPS habilita la consulta del formulario y un posible POST. Cookies y formulario quedan en memoria; los datos necesarios del contador de una sesión confirmada se conservan cifrados con DPAPI. Se validan certificados y se limitan los reintentos. Pausar cancela las operaciones.

Se sustituyó la comprobación basada en BindIPEndPointDelegate: con la resolución de esta conexión entraba en reintentos de enlace y consumía CPU. Ahora elige IPv4 e interfaz explícitamente, con conexiones acotadas y cancelación. Las sondas públicas solo admiten GET a dos URL exactas; los POST siguen limitados al portal.

Referencia: [Microsoft, IP_UNICAST_IF](https://learn.microsoft.com/en-us/windows/win32/winsock/ipproto-ip-socket-options).

60 pruebas locales correctas y prueba visual correcta. Diagnóstico real sin administrador: WireGuard con Internet antes y después, Internet directo por el router y formulario Nauta HTTP 200. La recuperación y el contador durante una sesión nueva real siguen pendientes.

## AutoNauta 2

- `RateMonitor.cs` muestrea contadores acumulados de interfaces físicas cada segundo. Usa reloj monotónico, evita picos por reinicios y no suma WireGuard con Ethernet. Las cifras son bits por segundo del equipo, incluidos LAN y sobrecarga de VPN.
- `AccountTime.cs` distingue tiempo disponible del historial y captura una sesión confirmada. El contador se consulta en EtecsaQueryServlet con op=getLeftTime y datos de sesión; no se autentica de nuevo para conseguirlo. La consulta de información verificada en vivo solo informó crédito en CUP, no tiempo: no se inventa una tarifa para convertirlo.
- El modo AutoLoginEnabled persiste y es independiente de la vigilancia. Desactivarlo cancela el ciclo pendiente antes de guardar el estado. Las consultas de cuenta son solo informativas y no pueden redirigirse a LoginServlet ni LogoutServlet.
- El indicador muestra solo un LED en reposo; expande una cápsula al entrar el mouse, mantiene el LED bajo el cursor y se repliega al salir. Incluye AUTO/MAN y chincheta. IndicatorPinned y la posición del LED se guardan.
- La configuración anterior se copia a `%LOCALAPPDATA%/AutoNauta`; el original no se borra. El mutex conserva su identidad anterior para impedir instancias paralelas de la versión antigua y nueva. Los datos de sesión también están cifrados; no se imprimen ni se incluyen en la entrega.
- La entrega v1 y fuentes anteriores se archivaron en `work/backup-v1`.

Referencia del protocolo de contador: [cliente Nauta publicado](https://github.com/raulodev/nauta/blob/main/src/nauta/client.py). El formato del formulario y la consulta de información se verificaron contra el portal real. El usuario confirmó que el contador aparece únicamente al iniciar una sesión nueva.

## Revisión de recursos 2.1

Informe actual: `outputs/AutoNauta/REVISION-RENDIMIENTO.md`. Se redujo trabajo de interfaz en modo LED, se agruparon notificaciones, se reutilizan iconos e inventario de adaptadores, se cachea DNS con invalidación, se acotan llamadas de red y redirecciones, se liberan tareas/eventos/suscripciones, se limitan registros y se evitan consultas de cuenta sin utilidad.

La versión anterior está en `work/backup-v2`. `work/PerformanceHarness.cs` permite comparar las dos bases de código con la misma carga sin usar credenciales. `work/Measure-Process.ps1` mide el proceso real externamente. No se redujo la frecuencia de velocidades ni se aumentó el intervalo de vigilancia para conseguir las mejoras.

## Repositorio y datos locales

Repositorio privado: [JoeSagera/AutoNauta](https://github.com/JoeSagera/AutoNauta). Rama principal: `main`.

Git conserva las fuentes, las instrucciones de compilación, el ejecutable de la entrega, documentación, imágenes de ejemplo y mediciones de recursos. Las copias `work/backup-v1` y `work/backup-v2`, pruebas temporales y diagnósticos de red permanecen solo en el equipo original; no forman parte de un clon del repositorio. Los dos instrumentos de medición en `work/` sí se versionan.

Las credenciales y sesiones cifradas están en `%LOCALAPPDATA%/AutoNauta`, fuera del proyecto. `.gitignore` excluye configuración personal, perfiles VPN, claves, registros y archivos temporales. Nunca añadas esos datos mediante `git add -f`.

## Corrección 2.1.1 — Mostrar escritorio

El LED podía minimizarse con el botón «Mostrar escritorio» y desaparecer de la pantalla. La ventana flotante ya no tiene estilos de minimizar/maximizar, ignora la orden de minimizar y se restaura, sin tomar el foco, si otra llamada la minimiza. La recuperación funciona por eventos; no añade temporizadores ni sondeos continuos.

El menú junto al reloj incluye **Recuperar LED**, que restaura solo el indicador. **Mostrar AutoNauta** también recupera un indicador oculto. Se conservan posición, fijación y estado de login.

Verificación: 60 pruebas correctas y prueba visual ampliada correcta, incluyendo 20 minimizaciones consecutivas, estilos nativos, recuperación sin cambiar el foco y barra fijada. El usuario confirmó en su Windows que el LED sigue visible al pulsar el botón real «Mostrar escritorio» después de instalar la versión 2.1.1.

Referencia del comportamiento de Windows: [Microsoft: WS_MINIMIZEBOX y Mostrar escritorio](https://devblogs.microsoft.com/oldnewthing/20241021-00/?p=110393).
