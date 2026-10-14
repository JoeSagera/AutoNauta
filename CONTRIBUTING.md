# Colaborar en AutoNauta

Para errores funcionales, abre un Issue con versión, Windows, tipo de conexión, pasos, resultado esperado y resultado observado. Indica si usas VPN, sin publicar su configuración. Los ejemplos y capturas deben ocultar cuentas, IPs, contraseñas y datos de sesión. Para fallas de seguridad, sigue [SECURITY.md](SECURITY.md).

Mantén cambios pequeños y documenta cómo los comprobaste. Ejecuta `Compilar.ps1` con AutoNauta cerrado; para interfaz, usa la vista previa con datos ficticios y las escalas 100 %, 150 % y 200 %. La guía de arquitectura está en [docs/DESARROLLO.md](docs/DESARROLLO.md).

No elimines la validación HTTPS, no amplíes los destinos de credenciales sin justificarlo y no pruebes reintentos con una cuenta real para provocar bloqueo. No añadas configuración personal, diagnósticos, archivos del portal ni perfiles VPN al repositorio. Los ZIP se publican como assets de una Release, no mediante Git.

Las contribuciones al proyecto se ofrecen bajo su licencia MIT. Usa únicamente código o recursos que puedas distribuir y conserva atribuciones/licencias cuando correspondan.
