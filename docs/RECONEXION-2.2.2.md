# AutoNauta 2.2.2 — reconexión y contador

Revisión del 13 de octubre de 2026, motivada por una caída que requirió login manual.

## Hallazgos observados

- El registro de la aplicación pasó por «Verificando caída» y «Esperando confirmación de Nauta», sin un intento de login. La condición que exigía una redirección HTTP detenía el flujo antes de consultar el portal. No se conservó la respuesta HTTP de esa caída, por lo que su causa exacta —DNS, timeout o formato— no puede determinarse retrospectivamente.
- Chrome tenía una sesión conectada en `/web/online.do`. La aplicación no tenía un contexto de sesión guardado.
- El cierre de sesión es un `input` cuyo valor es «Cerrar sesión», y no texto de un enlace. El reconocimiento anterior descartaba ese valor al eliminar las etiquetas HTML.
- `updateAvailableTime` envía diez campos a `/EtecsaQueryServlet`: op, ATTRIBUTE_UUID, CSRFHW, wlanuserip, ssid, loggerId, domain, username, wlanacname y wlanmac. El cliente anterior enviaba cuatro y tomaba el CSRF del formulario anterior al login.
- «Usuario ya conectado» debe tratarse como estado temporal; no demuestra contraseña incorrecta ni debe desactivar AUTO permanentemente.

## Cambio

Después de dos fallos de Internet, se conserva la sonda HTTP física. Si no confirma ni Internet ni redirección, se comprueba Internet con HTTPS por el router, con un límite total de 12 segundos. Si confirma Internet, no inicia sesión. Si tampoco confirma Internet, consulta el portal HTTPS aunque el navegador no haya abierto un popup. Un POST de login requiere el formulario validado, AUTO, credenciales guardadas y autorización de la política de reintentos.

La lectura del contador reproduce los parámetros literales de la página conectada, sin ejecutar JavaScript. Se usa el CSRF de esa página y se preserva su contexto. Se mantiene el destino HTTPS estricto, la validación del certificado y la prohibición de redirigir las consultas de tiempo a login/logout.

El menú del LED permite vincular un contador de una sesión del navegador desde un HTML guardado. Exige origen y cuenta correctos y una respuesta de tiempo válida del servidor antes de reemplazar la sesión anterior. El contexto queda cifrado mediante DPAPI para el usuario de Windows. Un archivo inválido o una consulta fallida conserva los datos anteriores.

## Validación

- **Medido:** 76 pruebas correctas, cero fallidas. Se usa el motor real con transporte sustituido en los escenarios controlados, sin cuentas reales ni servicios externos.
- **Medido:** caída sin redirección → formulario HTTPS → un login → Internet recuperado → contador válido. El primer fallo aislado no inicia sesión.
- **Medido:** fallo de la salida VPN con Internet físico → espera, cero logins. Internet funcional y MAN también evitan logins.
- **Medido:** contexto completo, CSRF posterior al login, cuenta distinta, parámetros duplicados, expresiones no literales, importación protegida y conservación de sesión tras importación fallida.
- **Medido en Nauta Hogar:** el parser reprodujo los diez campos observados y el cliente corregido obtuvo HTTP 200 con una duración válida. Se vinculó la sesión actual sin cerrar ni repetir el login.
- **Medido:** revisión de interfaz `--ui-smoke`, salida 0 y 17 capturas; conserva los controles de AUTO, fijación, minimización y ausencia de repintados en modo LED.
- **Confirmado por el usuario:** después de instalar la 2.2.2 y vincular la sesión existente, el tiempo aparece en el LED.
- **Pendiente:** observar una recuperación completa durante la siguiente caída real de Nauta. Las pruebas controladas no prueban por sí solas el comportamiento de ETECSA bajo una nueva caída.

Los archivos HTML, URL de sesión y respuestas particulares de esta inspección están en `work/`, excluidos de Git. La configuración protegida se conserva en la carpeta local de AutoNauta. No se incluyen cuentas, tokens, saldos personales ni páginas privadas en la documentación, ejecutable o paquetes para amigos.
