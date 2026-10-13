# AutoNauta 2 — validación, 12 de octubre de 2026

## Medido

- **50 pruebas locales correctas**, sin fallos. Incluyen HTTPS, formularios y CSRF, cifrado, pausa, cancelación, reintentos, modo de login independiente y persistente, sesión vinculada a la cuenta, conservación del contador al guardar sin cambiarla y eliminación al cambiarla.
- Las pruebas de velocidades comprueban deltas con tiempo real transcurrido, reinicio de contadores, interfaces nuevas y exclusión del túnel para evitar duplicados.
- Las pruebas de tiempo distinguen las horas restantes del historial, admiten más de 24 horas y rechazan valores inválidos. La consulta de información solo permite EtecsaQueryServlet: no puede redirigirse a iniciar ni cerrar una sesión.
- Prueba visual, salida 0 y diez capturas: ventana, minimizar/restaurar, LED ovalado sin texto ni esquinas rectas, barra expandida, posición del LED conservada, fijación, cuatro colores y botones AUTO/MAN y chincheta con acciones separadas.
- Prueba del motor con login automático desactivado: Internet por WireGuard confirmado, vigilancia activa y muestras de subida y bajada reales disponibles. No se enviaron credenciales en esa prueba.
- Diagnóstico de red final sin administrador: WireGuard con Internet antes/después y formulario Nauta HTTP 200 por Ethernet. Resultado en `diagnostico-validado.json`.
- Se abrió AutoNauta con la cuenta y contraseña cifrada migradas. El programa registró Internet conectado mediante WireGuard; la versión anterior quedó detenida y archivada.
- Consulta real de información de la cuenta: responde en EtecsaQueryServlet, informa crédito en CUP e historial de conexiones; no informa horas restantes. No se publicaron valores de cuenta ni tokens en la evidencia.

## Datos del contador

El usuario confirmó que Nauta muestra el contador al iniciar una sesión nueva y rechaza volver a conectar una cuenta ya conectada. AutoNauta no provoca una desconexión ni una autenticación repetida para obtenerlo.

El contador usa op=getLeftTime con los datos de la sesión capturados después de un login confirmado. Esos datos se guardan con DPAPI y se invalidan al cambiar de cuenta o perder la sesión. Se actualiza cada minuto; las velocidades se actualizan cada segundo. Una sesión iniciada antes de AutoNauta puede mostrar tiempo no disponible hasta la siguiente sesión nueva.

## Pendiente

- Login Nauta real durante una caída y comprobación del contador con esa sesión nueva. No se cerró la sesión actual para provocar la prueba.
- Arrastre físico entre monitores y observación de la expansión con el mouse real. Las pruebas del programa verifican geometría, estado y acciones de los controles.

Chrome no pudo inspeccionarse con el control disponible: la extensión falló y el control de Windows se detuvo al no poder verificar la URL con suficiente confianza. Se leyó el formulario en el navegador interno y se verificó la consulta mediante el cliente HTTPS del programa.

La imagen `barra-ejemplo.png` contiene datos de prueba. Las evidencias anteriores y la entrega v1 se conservan en `work/backup-v1`, fuera de los archivos de entrega actuales.

## Actualización 2.1

60 pruebas correctas y prueba visual ampliada correcta. Se compararon ambas versiones con LED y barra expandida, y se realizó una prueba de estabilidad de cinco minutos. Las medidas y el alcance de esta revisión están en `REVISION-RENDIMIENTO.md`; la versión 2 y su código anterior se conservaron en `work/backup-v2`.
