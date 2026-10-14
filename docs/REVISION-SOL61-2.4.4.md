# Segunda revisión — GPT-6.1 Sol Max y AutoNauta 2.4.4

13 de octubre de 2026. Revisión adicional solicitada por el usuario, realizada por un subagente **GPT-6.1 Sol con razonamiento Max**. Punto de partida: `f7bab9ca52b178affd4239775e01e150a4c86e6a`, AutoNauta 2.4.3, árbol limpio. El subagente revisó fuentes y ejecutó harness propios con datos ficticios; no leyó cuentas personales, no contactó Nauta ni manipuló la instancia activa o juegos.

## Resultado

No se confirmó una vulnerabilidad crítica o alta dentro del alcance revisado. Se confirmaron **dos defectos P2 y uno P3**, que las 95 pruebas anteriores no cubrían. Se corrigieron en 2.4.4 y se añadieron tres pruebas de regresión: **98 correctas, cero fallos**. Una suite que pasa no sustituye la revisión de escenarios que todavía no contiene.

## P2: repetición de consultas con credenciales tras HTTP 401/403

`Engine.RefreshAccount` podía enviar usuario y contraseña al endpoint informativo cuando no tenía contexto de contador. Si la respuesta era 401 o 403, salía por excepción antes de establecer el bloqueo de consultas. La siguiente consulta al minuto, o «Comprobar ahora», repetía el POST. En la reproducción sin red, cinco ciclos produjeron cinco POST y no activaron `accountQueryBlocked`. Un error equivalente dentro de HTML con HTTP200 sí bloqueaba la consulta.

Impacto: persistencia indebida ante un rechazo explícito. Podría contribuir a un bloqueo si el servidor cuenta esos rechazos, efecto **no probado contra el servidor real**. No se demostró envío a otro origen ni exfiltración de contraseña.

Corrección: 401/403 del POST informativo bloquean únicamente las consultas con contraseña y muestran un aviso. Conservan la vigilancia, el estado de Internet y el modo de login. Guardar la cuenta permite un nuevo intento; importar un contexto confirmado o capturar una nueva sesión válida recupera las consultas de tiempo. El token de cancelación se comprueba antes de aplicar la respuesta.

Verificación independiente: **un POST tras cinco vencimientos**, tanto para 401 como para 403; guardar cuenta permite un segundo POST y vuelve a bloquear si se rechaza. Verde conservado y bloqueo del login automático sin cambios. La prueba de regresión también verifica que una importación válida consulta el contador sin repetir la contraseña.

## P2: CPU acumulada pese al timeout por regex

El regex de atributos hacía trabajo costoso sobre nombres largos sin `=`. Cada `<input>` consumía menos de 250 ms, pero el documento contenía muchos y no existía un límite total. Sol reprodujo **37066 ms** para 907246 bytes y **38792 ms** al importar la misma carga de 907320 bytes, ambos por debajo de 1 MiB. El análisis dentro de la importación conservaba `cycleGate`, por lo que pausar o guardar cuenta podía esperar esos segundos.

Corrección: presupuesto monotónico de **un segundo por documento**, compartido por llamadas anidadas de reconocimiento/captura/importación y por el analizador de información. Cada regex conserva su timeout individual de hasta 250 ms, reducido al tiempo restante. Hay puntos de comprobación entre atributos, formularios, inputs y filas. El presupuesto se restaura al salir y se mantiene separado por hilo.

Verificación independiente posterior: la misma carga terminó en **1000 ms**, con rechazo controlado; importación en **992 ms**. Una carga alternativa de filas de 805200 bytes se rechazó en **1012 ms**. Un documento válido posterior se aceptó. Las pruebas separadas comprobaron que un scope anidado no renueva un presupuesto agotado, que las operaciones heredan el rechazo, que otro hilo no queda contaminado y que `Dispose` restaura el hilo original.

Los tiempos son mediciones de estas cargas en esta máquina. El presupuesto se revisa entre operaciones y depende de la comprobación cooperativa de timeout de .NET; **no es una garantía de tiempo real exacto de 1000 ms** ni convierte el parser en un parser HTML completo. Un documento que exceda el presupuesto se rechaza sin enviar credenciales ni guardar contexto.

## P3: AUTO efectivo atribuido a una sesión ya descartada

Una respuesta de login aceptada dejaba pendiente la atribución de éxito. Después de la gracia sin Internet, el portal podía volver a pedir login y descartarse el contexto; al estar en MAN no se enviaba otro login. Una recuperación independiente posterior todavía sumaba un AUTO efectivo de la sesión antigua.

Corrección: al reconocer un nuevo formulario de acceso después de la gracia, se cancela la atribución pendiente antes de retornar en MAN o permitir otro intento. Se conserva el conteo del login aceptado que todavía está esperando una recuperación temporal durante la gracia.

Verificación independiente: un POST de login, sesión descartada, MAN y recuperación independiente mantienen **AUTO efectivos = 0**. Las pruebas anteriores de aceptación y recuperación temporal siguen pasando.

## Validación de la entrega

- Núcleo: 98 pruebas locales correctas; compilación separada mientras seguía abierta la versión anterior.
- Interfaz: `--ui-smoke` a 100 %, 150 % y 200 %, salida 0 y 34 capturas por escala. El aviso de consulta rechazada también se muestra si se conserva un crédito previo.
- Instalación 2.4.4.0 con respaldo ignorado, igualdad SHA256 con candidato y conservación de cuenta, contraseña/contexto protegidos, modo, fijación y posición. Reiniciar para actualizar pone a cero los contadores de la ejecución.
- Paquete con ejecutable, guía, MIT y sumas SHA256, comprobado con `Validar-Paquete.ps1`. Los assets 2.4.3 se conservan y se publica una versión nueva.

Evidencia sintética completa conservada en el equipo bajo `work/revision-sol61-f7bab9c-security/`; no se distribuyen configuraciones o archivos privados con la Release.

## Cobertura y límites

Sol revisó TLS, destinos, redirecciones, cookies, DNS/interfaces, HTTP, HTML/importación, DPAPI/registros, cancelación/reintentos, estadísticas, guard de superposición, paquete y CI. No encontró un deadlock reproducible en los casos examinados ni confirmó el overflow ensayado en `DecodeChunks`. Su revisión del guard fue estática; no probó BF6 ni activó ventanas personales. Codex realizó las pruebas de interfaz de la entrega corregida por separado.

Se mantienen los límites publicados: ejecutable sin firma, revocación en línea no obligatoria antes de tener Internet, DPAPI no protege contra malware de la misma cuenta y usuario/preferencias legibles. No es pentest del servidor ni certificación de seguridad o estabilidad prolongada. Véase [SECURITY.md](../SECURITY.md).
