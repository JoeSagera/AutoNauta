# Verificación pública de AutoNauta 2.2.2

Fecha: 2026-10-13.

- Repositorio público: https://github.com/JoeSagera/AutoNauta
- Release beta publicada: https://github.com/JoeSagera/AutoNauta/releases/tag/v2.2.2
- ID de release: 412028938; borrador: false; prerelease: true.
- Fuente utilizada: `b39821f84c13e50dbaa71d3ab10f60b872d334c7`.
- Archivo: `AutoNauta-v2.2.2-windows-x64.zip`, 85454 bytes.
- SHA256: `766554268481040614607487e0348fb5f8a9e20a100a11bac5d7786e614d7e23`.
- Assets publicados: ZIP y `SHA256SUMS.txt`.

Se descargó el ZIP desde el enlace público sin autenticación. Su SHA256 coincide con el paquete local y con el digest informado por GitHub. El ZIP contiene únicamente `AutoNauta.exe`, `LEEME.txt` y `SHA256SUMS.txt`; el ejecutable extraído coincide con el compilado local. No incluye configuración, cuentas, contraseñas, sesiones ni páginas privadas del portal.

Validación funcional: 76 pruebas correctas, comprobación de interfaz y consulta real del contador; el usuario confirmó que el tiempo aparece en el LED. La instalación final quedó abierta con estado verde. La reconexión se verificó mediante escenarios controlados; su recuperación durante una nueva caída real sigue pendiente. Véase `docs/RECONEXION-2.2.2.md`.
