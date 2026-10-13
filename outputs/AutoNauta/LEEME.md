# AutoNauta

Entrega actual: **2.2.1.0**, con VPN opcional y el diseño de Claude revisado por Codex. La cuenta existente se conserva. La revisión de la interfaz y recursos 2.2 está en `REVISION-DISENO-2.2.md`.

## Empezar

Abre **AutoNauta.exe** y pulsa **Iniciar** en la fila **Vigilancia**. La cuenta y posición de la versión anterior se conservan; no necesita administrador ni VPN. Cada amigo introduce su propia cuenta antes de iniciar.

Con una cuenta guardada, la sección **Cuenta Nauta** muestra un resumen parcial del usuario. Para cambiarla, pulsa **Cambiar cuenta**, escribe el usuario completo de navegación y su contraseña, y pulsa **Guardar cuenta** o Intro. Dejar la contraseña vacía conserva la existente. **Cancelar** o Esc descarta lo escrito. **Eliminar cuenta** pide confirmación.

La X de la ventana y **Minimizar** dejan AutoNauta funcionando. **Salir** termina el programa. La computadora debe estar encendida y sin suspensión.

## Indicador ovalado

En reposo muestra solo el LED. Al pasar el mouse se extiende una barra con el tiempo restante, bajada **↓**, subida **↑**, modo de login y botón de fijación. Al retirar el mouse vuelve al LED.

Pulsa la chincheta para **mantener la barra expandida**. Vuelve a pulsarla para recuperar la ocultación automática. La misma opción está en el menú de clic derecho. Esta preferencia se guarda.

Arrastra el LED o el fondo de la barra para moverlo. Doble clic abre la ventana principal. La barra se extiende hacia el espacio disponible y conserva el LED en su posición.

- Verde: Internet confirmado por la salida actual de Windows.
- Amarillo: reconectando Nauta o esperando Internet / una acción manual.
- Rojo: error del portal, cuenta, certificado o acceso.
- Gris: pausado o esperando confirmación de la conexión.

## Login automático independiente

**AUTO** indica login automático activado; **MAN** indica desactivado. Pulsa ese botón en la barra, el interruptor **Login automático** de la ventana o la opción del menú para cambiarlo. La descripción de esa fila también indica AUTO o MAN.

Desactivarlo cancela los intentos pendientes y conserva la vigilancia, velocidades y consultas de tiempo. No inicia nuevas sesiones de navegación. Activarlo permite reconectar cuando se confirma que Nauta requiere acceso. El modo se guarda para la siguiente apertura.

**Pausar** detiene la vigilancia y las consultas de cuenta; las velocidades del sistema siguen visibles. Ninguna opción desconecta una sesión Nauta existente ni cambia la configuración de red. Una sesión abierta puede seguir consumiendo tiempo o saldo.

## Tiempo de la cuenta

Cuando AutoNauta inicia una sesión nueva, captura los datos del contador devuelto por Nauta y consulta el tiempo restante cada minuto. Los guarda cifrados para tu usuario de Windows para poder consultarlos al volver a abrir la aplicación.

Si te conectaste antes con Chrome u otro programa, AutoNauta puede no tener el identificador de esa sesión: muestra **No disponible** (en la 2.1.1, **—h —m**) hasta obtener un contador válido al iniciar una sesión nueva. No repite el login ni cierra tu sesión para conseguirlo. El portal de información devuelve crédito en CUP e historial; no convierte ese crédito a horas con una tarifa supuesta, ni confunde el historial con tiempo restante.

El tooltip muestra la hora de la última consulta. Si la actualización falla, conserva el último dato e indica el problema; no es una lectura nueva. Al cambiar de cuenta o terminar la sesión anterior, retira el contador anterior.

## Subida y bajada del sistema

Se actualizan aproximadamente cada segundo y corresponden al tráfico agregado de las interfaces físicas activas del equipo. Excluye WireGuard y otras interfaces virtuales para evitar duplicar sus bytes. Incluye tráfico de otras aplicaciones, tráfico local y la sobrecarga de la VPN; no es un test de velocidad ni mide otros dispositivos conectados al router.

Las unidades son **Kb/s, Mb/s o Gb/s**: bits por segundo. La primera muestra aparece tras disponer de dos lecturas de los contadores.

## Datos y límites

La configuración, contraseña cifrada, contador de sesión cifrado, posición y preferencias están en `%LOCALAPPDATA%\AutoNauta`. La configuración anterior se copia automáticamente desde la carpeta de ReconectorNauta sin borrar el original. No se registran contraseñas ni identificadores de sesión en los eventos.

HTTPS valida nombre, fechas y confianza del certificado; no exige una consulta de revocación en línea antes de tener Internet. Se mantienen los límites de reintentos y la espera ante respuestas ambiguas. No repara una línea ADSL caída ni se añadió inicio automático con Windows.

La recuperación real y la consulta del contador de una sesión nueva siguen pendientes de observar. Consulta `VALIDACION.md`. `barra-ejemplo.png` es una vista del diseño con cifras ficticias, no los datos de tu cuenta.

## Versión 2.1 y ejecución prolongada

La versión **2.1.1** introdujo la corrección de la desaparición del LED al pulsar «Mostrar escritorio»; la entrega 2.2 la conserva. Si necesitas restaurarlo, haz clic derecho en el icono de AutoNauta junto al reloj y elige **Recuperar LED**. **Mostrar AutoNauta** recupera el LED y abre la ventana principal. La corrección no incorpora vigilancia adicional ni cambia la cuenta o la VPN.

Con solo el LED visible, el programa evita repintados y actualizaciones de controles ocultos. La barra y las velocidades se actualizan al mostrarse. Conserva el muestreo de velocidades cada segundo y la vigilancia configurada.

Los registros conservan un archivo activo y cuatro archivos rotados, alrededor de 2,5 MiB en total. Una consulta de cuenta que no aporta el contador no se repite automáticamente; el siguiente login o **Comprobar ahora** permite consultar nuevamente.

Para abrir directamente minimizado puede ejecutarse `AutoNauta.exe --resume --background`. El doble clic normal mantiene la ventana inicial. No se añadió arranque automático con Windows.

Consulta `REVISION-RENDIMIENTO.md` para los cambios, medidas y límites de la prueba.

## Versión 2.2 — nueva interfaz

La ventana se organiza en estado, funcionamiento (vigilancia y login automático), cuenta y actividad reciente. Usa un tema oscuro, texto nítido con cualquier escala de Windows y una cápsula del LED más pequeña con bordes suavizados. Las funciones, el menú junto al reloj (**Recuperar LED** incluido), la posición guardada y las preferencias se mantienen. Detalles y capturas: `DISENO.md` y `diseno-2.2/`.

El ejecutable entregado ya es 2.2.1.0. Para una compilación de desarrollo, cierra AutoNauta con **Salir** y ejecuta `Compilar.ps1`; los amigos que reciben el paquete de Release solo tienen que extraerlo y abrir el programa.
