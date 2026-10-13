# AutoNauta

Programa Windows para recuperar la sesión Nauta Hogar automáticamente, con LED ovalado desplegable, contador de tiempo y tráfico del sistema. No requiere VPN; la cuenta cifrada existente se conserva.

## Descargar la beta

[AutoNauta 2.2.2 para Windows x64](https://github.com/JoeSagera/AutoNauta/releases/tag/v2.2.2). Descarga el ZIP, extrae su contenido y abre `AutoNauta.exe`; cada persona introduce su propia cuenta. No hace falta compilar ni instalar el programa. El contador de Nauta Hogar ya se verificó en una sesión real. Sigue siendo beta: la reconexión completa durante una nueva caída aún requiere validación de campo.

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

`SocketPath.Select` elige la interfaz de salida de cada socket IPv4 mediante IP_UNICAST_IF; solo afecta a ese socket. DNS del router, sonda HTTP cautiva y HTTPS Nauta usan la interfaz física. Desde 2.2.1 las sondas HTTPS de Internet usan la ruta actual de Windows para cada destino; el verde exige una respuesta exacta y no requiere VPN.

Tras dos fallos, una petición HTTP sin credenciales distingue Internet directo de una redirección al portal. Si no hay redirección, la versión 2.2.2 comprueba también Internet por la interfaz física con HTTPS. Si el router tiene Internet, espera a recuperar la salida del sistema sin autenticar. Si ambas comprobaciones fallan, consulta el portal HTTPS aunque no haya popup y solo envía el login si reconoce el formulario Nauta y AUTO está activado. Cookies y formulario quedan en memoria; los datos necesarios del contador de una sesión confirmada se conservan cifrados con DPAPI. Se validan certificados y se limitan los reintentos. Pausar cancela las operaciones.

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

Repositorio: [JoeSagera/AutoNauta](https://github.com/JoeSagera/AutoNauta). Rama principal: `main`. El propietario autorizó hacerlo público para compartir las Releases con sus amigos.

Git conserva las fuentes, las instrucciones de compilación, el ejecutable de la entrega, documentación, imágenes de ejemplo y mediciones de recursos. Las copias `work/backup-v1` y `work/backup-v2`, pruebas temporales y diagnósticos de red permanecen solo en el equipo original; no forman parte de un clon del repositorio. Los dos instrumentos de medición en `work/` sí se versionan.

Las credenciales y sesiones cifradas están en `%LOCALAPPDATA%/AutoNauta`, fuera del proyecto. `.gitignore` excluye configuración personal, perfiles VPN, claves, registros y archivos temporales. Nunca añadas esos datos mediante `git add -f`.

## Corrección 2.1.1 — Mostrar escritorio

El LED podía minimizarse con el botón «Mostrar escritorio» y desaparecer de la pantalla. La ventana flotante ya no tiene estilos de minimizar/maximizar, ignora la orden de minimizar y se restaura, sin tomar el foco, si otra llamada la minimiza. La recuperación funciona por eventos; no añade temporizadores ni sondeos continuos.

El menú junto al reloj incluye **Recuperar LED**, que restaura solo el indicador. **Mostrar AutoNauta** también recupera un indicador oculto. Se conservan posición, fijación y estado de login.

Verificación: 60 pruebas correctas y prueba visual ampliada correcta, incluyendo 20 minimizaciones consecutivas, estilos nativos, recuperación sin cambiar el foco y barra fijada. El usuario confirmó en su Windows que el LED sigue visible al pulsar el botón real «Mostrar escritorio» después de instalar la versión 2.1.1.

Referencia del comportamiento de Windows: [Microsoft: WS_MINIMIZEBOX y Mostrar escritorio](https://devblogs.microsoft.com/oldnewthing/20241021-00/?p=110393).

## Rediseño 2.2 — interfaz

Ventana principal en cuatro bloques (estado, funcionamiento, cuenta y actividad) con tema grafito y un único acento azul. Indicador con transparencia por píxel (cápsula más pequeña y con bordes suavizados) y proceso de interfaz *DPI-aware*. La posición se sigue guardando en unidades de 96 DPI, como en las versiones anteriores. No se modificaron la conexión, la autenticación, la VPN, el cifrado ni las métricas.

Fuentes nuevos: `src/UiTheme.cs` y `src/UiMain.cs`. Decisiones, capturas y pendientes: `outputs/AutoNauta/DISENO.md`, `outputs/AutoNauta/diseno-2.2/` y la sección 2.2 de `VALIDACION.md`. La entrega `AutoNauta.exe` ya está compilada como **2.2.0.0**. Codex revisó el diseño de Claude, ejecutó las 60 pruebas y la revisión visual a 100 %, 150 % y 200 %, y comparó recursos con la 2.1.1. Resultados: `outputs/AutoNauta/REVISION-DISENO-2.2.md`.

## Release 2.2.1 — VPN opcional

La versión **2.2.1.0** introdujo `Connectivity.CheckCurrent`, que elige la interfaz de salida que Windows indica para cada sonda HTTPS, sin exigir WireGuard ni otro cliente. El portal conserva su acceso separado por la interfaz física. No se cambia ni desconecta una VPN existente y no se declaran conectadas las respuestas de un portal cautivo.

Las descripciones anteriores de comprobación obligatoria por VPN son el contexto histórico de las versiones 2.1 y 2.2. Desde 2.2.1 el indicador verde confirma Internet por la salida actual del sistema. Pasan 65 pruebas, incluidas salida sin VPN, salida de túnel, ausencia de ruta, portal cautivo y cancelación.

`Preparar-Release.ps1` empaqueta únicamente el ejecutable, `docs/GUIA-AMIGOS.txt` y sumas SHA-256 en `outputs/releases/v2.2.1/`. Los ZIP se adjuntan a GitHub Releases; no incluyen configuración, cuentas, sesiones, perfiles VPN ni diagnósticos locales. Notas de la beta: `docs/releases/v2.2.1.md`.

## Corrección 2.2.2 — reconexión sin popup y contador Hogar

El registro de una caída mostró que la 2.2.1 se quedaba esperando una redirección HTTP que no llegó. La 2.2.2 consulta el formulario HTTPS después de comprobar que tampoco hay Internet físico; el popup del navegador deja de ser un requisito. El aviso «usuario ya conectado» es una espera temporal y conserva AUTO.

La página real `/web/online.do` usa un botón `input` para cerrar sesión y un contexto de diez campos para `getLeftTime`. Se reconoce ese control y se captura el CSRF de la página conectada junto con UUID, usuario, wlanuserip, ssid, loggerId, domain, wlanacname y wlanmac. La consulta corregida devolvió HTTP 200 y una duración válida en Nauta Hogar.

Para una sesión iniciada manualmente, el menú del LED incluye **Vincular contador del navegador…**: importa el HTML guardado desde la página conectada, exige la misma cuenta y confirma el tiempo con Nauta antes de guardar los datos cifrados. No ejecuta JavaScript ni inicia/cierra una sesión. Los HTML personales permanecen fuera del repositorio.

Pasaron 76 pruebas, incluida una caída sin redirección con login, recuperación y contador; también se comprueba que una caída de VPN con Internet físico no envía login. Validación y límites: `docs/RECONEXION-2.2.2.md`. La recuperación durante la siguiente caída real sigue pendiente de observar.

## LED lateral discreto (2.3.0)

En verde y sin interacción, el indicador se esconde en el lateral más cercano de su monitor como una muesca de 7×22 px y revela la barra al pasar el mouse. En amarillo, rojo o gris vuelve en el acto a la posición del usuario. La cápsula pasa a 32×26 px y la barra a 400×26 px. Comportamiento, medidas, validación y pendientes: `outputs/AutoNauta/LED-LATERAL.md` y `outputs/AutoNauta/diseno-lateral/`.
