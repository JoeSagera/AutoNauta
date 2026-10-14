# Seguridad de AutoNauta

## Informar una vulnerabilidad

Usa [Report a vulnerability](https://github.com/JoeSagera/AutoNauta/security/advisories/new) para informar en privado al mantenedor. Incluye versión, pasos reproducibles, efecto y un ejemplo con datos ficticios. No adjuntes contraseñas, `config.json`, cookies, HTML real de Nauta, UUID o CSRF de una sesión.

Los problemas normales de funcionamiento pueden ir en [Issues](https://github.com/JoeSagera/AutoNauta/issues). Si existe posible exposición de credenciales o un ataque reproducible, utiliza el canal privado antes de publicarlo. No hay un plazo garantizado de respuesta ni programa de recompensas.

## Versiones

La versión de referencia para correcciones es la beta pública más reciente. Las anteriores se conservan como historial y pueden tener problemas ya corregidos. Descarga el ZIP y la suma SHA256 desde la misma Release de este repositorio.

## Protección y límites

- Contraseña y contexto de sesión: DPAPI `CurrentUser`, guardados en `%LOCALAPPDATA%\AutoNauta`. Usuario, IP recordada y preferencias: texto legible. DPAPI no protege frente a malware que ejecute con tu misma cuenta de Windows; durante el login hay datos sin cifrar en memoria.
- TLS 1.2: se comprueban nombre DNS, vigencia y cadena de confianza. No se acepta cualquier certificado y no se fija una huella de certificado. La revocación en línea no se exige antes de tener Internet; no se garantiza detectar un certificado revocado en ese momento. Un certificado de confianza instalado en Windows puede afectar esa validación.
- Credenciales: el formulario reconocido y el tráfico de login se restringen al origen `https://secure.etecsa.net:8443/`. No se envían a las sondas públicas ni a una redirección externa. El contador se consulta mediante el endpoint informativo y no ejecuta el JavaScript de la página importada.
- Los intentos están espaciados y limitados. Credenciales rechazadas, CAPTCHA o cuota insuficiente requieren revisión manual. Un portal comprometido en el origen legítimo queda fuera de la garantía de esta aplicación.
- Red: solo conexiones salientes; no abre un servidor ni cambia rutas del sistema. Las sondas de conectividad usan Microsoft/Google. La sonda HTTP cautiva no contiene credenciales y su respuesta no equivale a una conexión HTTPS autenticada.
- Inicio con Windows (desde 2.4.5): activado por defecto y configurable. Escribe únicamente la entrada `AutoNauta` en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, con la ruta absoluta entre comillas y `--startup`; desactivarlo elimina esa entrada. No instala un servicio ni pide privilegios de administrador. Mantén la carpeta del ejecutable protegida como cualquier programa que se abra al iniciar sesión. Si deshabilitas AutoNauta desde el Administrador de tareas, Windows puede impedir el inicio aunque la preferencia de la aplicación siga activada.
- Registros de actividad: mensajes propios, sin volcar respuestas HTML, cuerpos de login o tokens de sesión. Diagnósticos de red y HTML guardados por el usuario pueden contener identificadores, IPs y datos privados; no deben compartirse sin revisar.
- El ejecutable no está firmado. SHA256 permite comparar archivos, pero no protege si un atacante sustituye simultáneamente el paquete y la suma en la cuenta de distribución.

La revisión del código y las pruebas no equivalen a una certificación ni demuestran ausencia de vulnerabilidades. Consulta [la revisión de Luna para 2.4.3](docs/SEGURIDAD-RELEASE-2.4.3.md) y [la segunda revisión de Sol para 2.4.4](docs/REVISION-SOL61-2.4.4.md) para ver alcance, resultados, correcciones y limitaciones. Actualiza a 2.4.4 o posterior: 2.4.3 todavía tenía casos de consumo acumulado de CPU y repetición de consultas con credenciales tras un rechazo HTTP.
