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

## Actualización 2.1.1 — 13 de octubre de 2026

- 60 pruebas locales correctas, cero fallos. Prueba visual ampliada: salida 0 y diez capturas.
- Comprobados los estilos nativos del LED sin minimizar/maximizar, la orden SC_MINIMIZE ignorada y la restauración ante una minimización forzada. Se repite esta última 20 veces manteniendo posición y visibilidad.
- «Recuperar LED» restaura una ventana oculta sin abrir la principal ni cambiar la ventana con foco. «Mostrar AutoNauta» también restaura el indicador. Se comprueba que una barra fijada conserva su fijación y tamaño después de minimizarse.
- **Confirmación manual del usuario:** después de instalar la corrección, pulsó el botón real «Mostrar escritorio» y respondió «El LED sigue visible».
- Se reinició la aplicación minimizada con la configuración existente. No se cambiaron las conexiones Nauta ni WireGuard. La versión anterior y los fuentes cambiados permanecen en `work/backup-showdesktop`, solo local.

La comprobación directa de Win+D no se ejecutó; la confirmación manual corresponde al botón de la barra de tareas. Las mediciones de rendimiento anteriores corresponden a 2.1; esta corrección responde a eventos y no añade sondeos ni temporizadores.

## Rediseño 2.2 — 13 de octubre de 2026

Validación inicial de Claude sobre los fuentes compilados en una carpeta temporal. En esa fase **no se reemplazó `AutoNauta.exe`**, que seguía abierto (2.1.1). No se tocaron WireGuard ni la sesión Nauta. La revisión e integración posteriores de Codex están documentadas en `REVISION-DISENO-2.2.md`; la entrega actual ya es 2.2.0.0.

### Comprobado

- **Pruebas locales:** 60 correctas, 0 fallidas, compiladas con `src/` completo.
- **`--ui-smoke` a la escala real (150 %):** salida 0 en cinco ejecuciones seguidas y 17 capturas. Se mantienen todas las comprobaciones de la 2.1.1: estilos nativos, `SC_MINIMIZE` ignorado, 20 minimizaciones forzadas, **Recuperar LED** sin cambiar el foco, barra fijada que conserva su tamaño, ausencia de actualizaciones con solo el LED, ausencia de redibujados con cifras sin cambio y reutilización de iconos. Comprobaciones nuevas:
  - ventana *layered* y límites de la ventana iguales a la superficie dibujada, también después de restaurar;
  - reposo en cápsula ovalada, con esquinas transparentes y centro opaco;
  - un estado repetido no redibuja el LED;
  - en la esquina derecha la barra se abre hacia la izquierda; en la izquierda, hacia la derecha. En ambos casos queda dentro de la pantalla y el LED no se mueve;
  - tras una ronda de calentamiento, 300 ciclos de expansión, cifras y estados no aumentan los objetos GDI ni de usuario;
  - el resumen de cuenta no muestra el usuario completo; **Cambiar cuenta**/**Cancelar** abren y cierran el formulario y descartan la contraseña;
  - en una ventana de 420×420 todos los controles siguen accesibles con desplazamiento.
- **`--ui-smoke` con `AUTONAUTA_UI_SCALE`=1, 1,25 y 2:** salida 0. Se revisaron las capturas a 100 % y 200 %.
- **Mouse real sobre `--preview`:** al llevar el cursor al LED, la cápsula pasó de 69×45 a 639×45 px abriéndose hacia la izquierda, con el borde del LED fijo y sin cambiar la ventana activa. Al retirar el cursor volvió a 69×45. Las capturas de esta prueba incluían el escritorio del usuario y no se guardaron en el proyecto.
- `work/PerformanceHarness.cs` compila contra la nueva interfaz. No se ejecutó porque inicia el motor con sondas reales.
- Se revisaron las capturas de ventana (primer uso, cuenta guardada, error, tamaño mínimo, 100 %), menú e indicador sobre fondos claro, azul y negro. Se corrigieron texto truncado en la actividad, descripciones cortadas, titular compitiendo con un botón y espacio de «No disponible».

Las primeras repeticiones fallaron al reutilizar una carpeta de capturas recién borrada (error genérico de GDI+ al guardar). Con una carpeta nueva por ejecución, todas pasaron. El programa normal no escribe capturas.

### Pendiente de prueba manual

- Compilar con `Compilar.ps1` tras cerrar la 2.1.1 y comprobar que el LED aparece en la posición guardada.
- **Mostrar escritorio** real y Win+D con la ventana *layered*: las pruebas reproducen la minimización forzada, no el botón del shell.
- Arrastrar el LED con el mouse físico a las cuatro esquinas y entre monitores.
- Uso prolongado con la red real y comparación de recursos con `work/Measure-Process.ps1`.
- Monitores con escalas distintas y lector de pantalla.

## Release 2.2.1 — VPN opcional

El usuario autorizó publicar el repositorio completo y pidió retirar la exigencia de WireGuard. La comprobación de Internet ahora selecciona la ruta IPv4 actual de Windows para cada sonda HTTPS; Nauta sigue usando el acceso físico por el router. No se cambian rutas, clientes VPN ni el criterio que autoriza enviar credenciales.

65 pruebas correctas. Las cinco nuevas cubren ruta física sin VPN, ruta de túnel, ausencia de interfaz, una respuesta cautiva que no debe dar verde y cancelación antes/durante la comprobación. Estas pruebas de selección usan dependencias controladas: no se desconectó la VPN del usuario para simular una red sin VPN.

El ZIP preparado incluye solo ejecutable, guía y sumas SHA-256. Se extrajo a una carpeta nueva, se verificó que el ejecutable coincide por hash y se ejecutó la prueba visual del programa extraído: salida 0 y 17 capturas. La recuperación durante una caída real y el contador de una sesión nueva siguen pendientes de observación.

Antes del ajuste final de textos, una medición de 60,57 segundos de la 2.2.1 minimizada registró 0,184 % de CPU total y picos de 57,80 MiB residentes y 30,10 MiB privados. Es una muestra corta del proceso real, no una comparación controlada con las anteriores ni una garantía de consumo fijo. El JSON se conserva en `evidencias-rendimiento-2.2.1/`.

## Corrección 2.2.2 — reconexión y contador Hogar

El usuario informó una caída que tuvo que resolver mediante login manual. Se corrigió la condición que impedía consultar Nauta sin redirección HTTP, el reconocimiento del botón de cierre en la página conectada y el contexto incompleto de la consulta de tiempo. El estado temporal «usuario ya conectado» conserva AUTO.

76 pruebas correctas, incluyendo el flujo completo controlado de caída sin popup, login, Internet y contador; el caso de fallo VPN con Internet físico evita el login. `--ui-smoke` terminó con salida 0 y 17 capturas. El cliente y parser corregidos reprodujeron los diez campos de la página real y obtuvieron HTTP 200 con una duración válida de Nauta Hogar.

La sesión manual existente se vinculó y el usuario confirmó que el tiempo aparece en el LED. No se cerró ni repitió su login. Para otras sesiones del navegador se añadió «Vincular contador del navegador…» al menú del LED. No se incluyeron las páginas personales ni sus tokens en la entrega.

La recuperación completa durante la siguiente caída real sigue pendiente. Detalles y límites: `../../docs/RECONEXION-2.2.2.md`.

## LED lateral discreto — 13 de octubre de 2026

Entrega de diseño sobre 2.2.2; detalles en `LED-LATERAL.md`.

- **Pruebas funcionales:** 76 correctas, 0 fallidas.
- **`Compilar.ps1`:** correcto. La instancia personal de `outputs/releases/v2.2.1/` siguió en ejecución, sin cerrarla ni reemplazarla.
- **`--ui-smoke`:** salida 0 a 100 %, 150 % (dos ejecuciones) y 200 %, más el ejecutable oficial; 26 capturas en cada ejecución. Incluye ambos laterales, cambios de estado durante la animación, gris, fijación, menú, arrastre, «Recuperar LED», minimización forzada y control de objetos GDI.
- **Mouse real sobre `--preview` en verde de ejemplo:** revela la barra sin cambiar el foco y vuelve a la muesca después del retardo.
- **Consumo de la interfaz en reposo con la muesca:** 0 ms de CPU en 30 s y GDI estable.
- **Pendiente de prueba manual:** «Mostrar escritorio» y Win+D reales, juegos (sin verificar sobre pantalla completa exclusiva), varios monitores reales, barra de tareas lateral real y uso prolongado.

### Integración independiente 2.3.0

Codex compiló e instaló 2.3.0.0. Pasaron las 76 pruebas, las pruebas visuales a 100/150/200 % y una ejecución final a 150 %. El usuario confirmó muesca en verde, revelación con contador y acceso tras «Mostrar escritorio»: «Sí, todo funciona bien». Medición con vigilancia activa: aproximadamente 0,052 % de CPU total en una muestra de 60 s, con GDI y objetos de usuario estables. Evidencia, límites y revisión: `REVISION-LED-LATERAL-2.3.md`.
