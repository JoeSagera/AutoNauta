# Revisión de seguridad de AutoNauta 2.4.3

13 de octubre de 2026. Revisión local del código, historial y distribución antes de publicar la beta. Codex revisó la publicación y los flujos de datos; un subagente **GPT-6 Luna, razonamiento Max**, solicitado por el usuario, realizó una revisión de seguridad y reproducciones sintéticas separadas. No es una auditoría certificada ni una prueba de ausencia de vulnerabilidades.

## Hallazgo confirmado y corregido

**P2 / severidad media: bloqueo por HTML malformado (ReDoS).** Los regex de comentarios, scripts y formularios de `PortalParser.Parse` no tenían timeout. Miles de aperturas sin cierre provocaban crecimiento cuadrático del tiempo de análisis. La importación de una página seleccionada por el usuario también llegaba a ese analizador. Luna reprodujo varios segundos de CPU con solo 24–30 KB de HTML ficticio; el límite de 1 MiB no limitaba el tiempo. No se reprodujo robo de credenciales ni ejecución de código.

Corrección: `HtmlRegex`, en `BoundedInput.cs`, aplica un timeout de 250 ms a cada operación regex sobre HTML en `Core.cs` y `AccountTime.cs`. Los límites se capturan en el reconocimiento del portal, captura/importación de contexto y lectura del contador. Un timeout devuelve rechazo controlado, sin enviar login ni guardar contexto. El límite es por operación, no una garantía de 250 ms para todo un documento bien formado.

**Verificación separada de Luna:** con entradas hostiles de 1 MiB, el reconocimiento de formulario/script/comentario terminó aproximadamente en 321/245/250 ms; la importación en 293 ms, captura de sesión en 292 ms y contador en 248 ms. Documentos bien formados de 1 MiB con muchos elementos terminaron aproximadamente en 408–522 ms. Estas son mediciones de esta máquina y esa carga, no garantías universales.

Se añaden pruebas de rechazo para comentarios/scripts/formularios sin cierre, importación y filas de contador costosas. El conjunto del núcleo pasa **95 pruebas, cero fallos**. Luna compiló y ejecutó también la suite desde las fuentes corregidas.

## Endurecimiento adicional

- La importación lee desde un único `FileStream`, con `FileShare.Read`, comprobación inicial y límite durante toda la lectura de 1 MiB. Se preserva UTF8/BOM. Ya no depende de comprobar el tamaño de una ruta y después abrirla otra vez sin cota de lectura. Se admitió exactamente 1 MiB y se rechazó 1 MiB + 1. El posible cambio entre las dos aperturas anteriores se consideró un riesgo local; no se afirmó una explotación observada.
- Se libera la referencia de redacción `enteredSecret` después de guardar o fallar, sin prometer borrado completo de strings del heap de .NET. DPAPI y los datos en memoria durante login conservan sus límites descritos en SECURITY.md.
- Se ignoran HTML/HTM además de configuración, claves, perfiles VPN y logs. El ZIP tiene una lista exacta: ejecutable, guía, licencia MIT y sumas SHA256; `Validar-Paquete.ps1` comprueba contenido, versiones e integridad.
- GitHub confirmó **secret scanning**, **push protection** y **reportes privados de vulnerabilidades** activados. Estas protecciones no sustituyen la revisión local y no garantizan detectar todos los secretos.
- La compilación y las pruebas del núcleo/paquete se automatizan en Windows con permisos `contents: read`. El checkout usa una revisión fijada por SHA y no conserva credenciales en el checkout del runner.

## Alcance y resultados restantes

Se revisaron TLS/certificados, allowlists, redirecciones, construcción de formularios, contexto de sesión, DPAPI, red y sockets, parser HTTP, registros, importación, procesos de interfaz y empaquetado. No se encontró una falla crítica o alta explotable en los casos revisados. Las pruebas se hicieron con datos ficticios, sin enviar login al portal real.

El posible overflow de longitudes en `DecodeChunks` se comprobó con una carga sintética; la cota basada en longitud `long` rechazó la entrada con `IOException`. No se confirmó una explotación y no se contabiliza como vulnerabilidad corregida.

Revisión de privacidad: **13 commits y 192 blobs únicos** alcanzables en el historial existente, más los archivos del árbol preparado. Sin coincidencias con datos personales conocidos ni patrones seleccionados de tokens, claves privadas o configuración cifrada personal. Las capturas usadas en documentación son de vista previa con datos ficticios. El resultado no equivale a un detector universal de secretos ni revisa contenido inaccesible/eliminado fuera del historial alcanzable.

La interfaz se comprueba a 100 %, 150 % y 200 %, incluidos recuperación de prioridad, ocultamiento, menús y recursos. Se corrigió además una comprobación de prioridad innecesaria cuando el indicador está oculto o suspendido: no se encolan esos avisos ni se inicia la comprobación diferida en esa situación.

Límites aceptados: ejecutable sin firma digital; revocación TLS en línea no obligatoria antes de tener Internet; malware con la misma cuenta de Windows puede usar DPAPI; nombre de usuario/preferencias legibles; confianza en el origen legítimo de ETECSA y almacén de certificados de Windows. No hubo pentest del servidor, análisis del kernel/anticheat, pruebas en todos los sistemas o certificación de estabilidad prolongada. Consulte [SECURITY.md](../SECURITY.md).

La documentación pública distingue pruebas, confirmaciones del usuario y límites. El usuario confirmó reconexión real, contador y visibilidad encima de BF6 en su configuración; siguen siendo observaciones limitadas y la entrega conserva la etiqueta beta.
