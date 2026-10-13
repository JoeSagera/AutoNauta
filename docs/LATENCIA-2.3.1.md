# AutoNauta 2.3.1 — respuesta a cambios de red

13 de octubre de 2026.

## Problema observado y causa en el código

El usuario midió aproximadamente 6 s hasta mostrar falta de conexión al retirar Ethernet y 16 s hasta volver a verde al reconectar. Esos tiempos son observaciones del usuario, no mediciones instrumentadas del motor.

El motor espera al menos 15 s entre ciclos normales. Ya recibía notificaciones de cambios de red desde `RateMonitor`, pero el manejador solo borraba la caché DNS y activaba el evento de despertar. Una petición en curso podía seguir esperando: DNS admite hasta 3 s y cada sonda pública HTTPS hasta 5 s, con varios destinos posibles. Además, el segundo fallo de conectividad esperaba el intervalo normal. Estos factores explican demoras posibles; no se reconstruyó el desglose exacto de los dos tiempos comunicados.

## Cambio implementado

- El aviso de red cancela el ciclo en curso y despierta al trabajador. Se vuelve a comprobar la salida actual de Windows; no se fuerza ni desconecta la VPN.
- Antes de enviar sondas, se comprueba si existe Ethernet o Wi-Fi físicos activos, con IPv4 utilizable y puerta de enlace. Un túnel virtual activo no basta. Si falta ese enlace, se muestra amarillo **«Sin conexión de red»** sin esperar DNS, HTTPS ni enviar login. Se conserva la sesión del contador.
- Tras un aviso, se activa durante 20 s una espera máxima de 2 s entre ciclos fallidos. Cuando hay verde se recupera inmediatamente el intervalo normal. Después del primer fallo de Internet también se hace la segunda comprobación tras 2 s, manteniendo el requisito de confirmar dos fallos.
- La falta de enlace borra solo el contador de fallos de conectividad, evitando dejar activo indefinidamente ese segundo intento rápido. No borra el bloqueo de autenticación, su contador ni la espera mínima entre logins.
- La sonda inicial que termina después de cancelar su ciclo no puede declarar verde ni reiniciar el contador de fallos de autenticación.
- No hay un nuevo temporizador permanente ni un bucle de sondeo adicional: se reutilizan el trabajador, su evento y las notificaciones existentes. La comprobación local del enlace se hace al comenzar un ciclo, no cada cuadro ni cada actualización de velocidades.

La suscripción existente utiliza `NetworkAddressChanged` y `NetworkAvailabilityChanged`. Microsoft documenta los cambios de dirección de interfaz, incluidos los que pueden originarse al desconectar un cable: [NetworkChange de .NET Framework](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.networkchange?view=netframework-4.8.1). No se supone que el sistema notifique todos los cambios con una latencia garantizada.

## Validación

- **83 pruebas correctas, cero fallos**, compilando desde las fuentes. Las siete nuevas pruebas comprueban falta de enlace con VPN aparentemente activa, cancelación de una petición que devolvería un verde tardío, despertar al reconectar, recuperación tras 2 s sin otro aviso, pausa, conservación de límites de login y avisos posteriores al cierre.
- Las pruebas de desconexión y despertar usan el trabajador real con servicios simulados y exigen respuesta inferior a 1,5 s desde la notificación simulada; no representan tiempos de Windows, DNS, ADSL o VPN reales.
- El caso de disponibilidad que llega después del aviso se recupera sin otra notificación, en la siguiente comprobación rápida, con límite de prueba de 3,5 s.
- La prueba visual `--ui-smoke` a 150 % terminó con salida 0 y 26 capturas. No cambió el código del diseño del LED.
- Se instaló **2.3.1.0** con copia anterior local de 2.3.0. Se comprobó igualdad del ejecutable compilado e instalado y conservación de cuenta, contraseña y sesión protegidas, modo, fijación y posición.

## Consumo observado

Comparación simultánea de 2.3.0 y el candidato 2.3.1 con el instrumento existente, sin credenciales ni login automático, 20 s de calentamiento y unos 60 s de medición. Ambas instancias terminaron con Internet comprobado y la muesca oculta; 16 procesadores lógicos. Datos completos: `outputs/AutoNauta/evidencias-latencia-2.3.1/`.

| Medida | 2.3.0 | Candidato 2.3.1 |
|---|---:|---:|
| Duración | 60,20 s | 60,12 s |
| Tiempo de CPU | 0,375 s | 0,469 s |
| CPU sobre todo el equipo | 0,039 % | 0,049 % |
| Memoria residente final | 56,04 MiB | 56,23 MiB |
| Memoria privada final | 31,77 MiB | 31,91 MiB |
| GDI inicial/final | 51 / 51 | 51 / 51 |
| Objetos de usuario inicial/final | 69 / 69 | 69 / 69 |

Son muestras breves del estado conectado, no una garantía de consumo máximo ni de uso durante días. La medición precede al ajuste final que reinicia el contador de fallos cuando falta enlace; ese ajuste afecta al estado desconectado, no al recorrido conectado medido.

## Comprobación de campo y límites

El usuario repitió la prueba con 2.3.1 y confirmó: al desconectar, el cambio de estado fue **casi instantáneo**; al reconectar, verde en **aproximadamente 9 s**, frente a los 16 s anteriores. Son tiempos aproximados comunicados por el usuario, con VPN; no hay una marca instrumentada del instante físico de insertar el cable.

El registro local muestra transiciones posteriores al aviso de red de aproximadamente 1–2 s hasta verde. Por ejemplo, aviso a las 17:07:19 y verde por VPN a las 17:07:20; otro aviso a las 17:09:37 y verde por VPN a las 17:09:39. También hay comprobaciones por Ethernet que pasan de aviso a verde en alrededor de 1 s. El log tiene resolución de segundos, por lo que estos intervalos son aproximados. No se deduce de ellos qué fracción de los 9 s corresponde a detección de enlace, DHCP, rutas o recuperación del túnel.

La VPN puede contribuir: WireGuard mantiene su propio intercambio de claves, temporizadores de reintento y recuperación automática, descritos en su [protocolo oficial](https://www.wireguard.com/protocol/). No se midieron sus intercambios ni se atribuyen todos los 9 s a WireGuard. AutoNauta verifica Internet por la ruta que utiliza Windows, incluida la VPN si esa es la salida actual.

El verde sigue requiriendo una respuesta HTTPS válida de Internet. Insertar el cable no demuestra que Windows tenga IP/ruta ni que WireGuard, el router y ADSL hayan recuperado servicio. La ventana de recuperación acorta la espera entre ciclos; no limita a 2 s la duración de sus peticiones de red. Si Windows no emite un aviso, queda la comprobación periódica normal. Una pérdida de ADSL que mantiene Ethernet activo se detecta mediante las sondas, no por el cable.

Esta revisión no cerró la sesión Nauta ni cambió la configuración de la VPN. La recuperación de un login tras una nueva caída real de Nauta sigue siendo una comprobación distinta.

Paquete local preparado: `outputs/releases/v2.3.1/AutoNauta-v2.3.1-windows-x64.zip`, 94089 bytes. SHA256: `1838182608f41d89233fd55f5e2fd4ab9e88e06ee2e600307659bc3962550952`. Contiene solo ejecutable, guía y sumas de comprobación; no se anuncia una nueva Release publicada.
