# Publicación verificada — AutoNauta 2.4.3 beta

13 de octubre de 2026.

- Release pública: [AutoNauta 2.4.3](https://github.com/JoeSagera/AutoNauta/releases/tag/v2.4.3), no borrador, marcada como prerelease/beta.
- Tag anotado `v2.4.3`, commit comprobado `fbc70aaf27428dcea9ec35cc396232190650e138`.
- [Compilar y validar en GitHub](https://github.com/JoeSagera/AutoNauta/actions/runs/38501560824): resultado **success** para ese commit. Compilación, pruebas del núcleo, preparación y verificación de contenido/SHA256.
- Paquete `AutoNauta-v2.4.3-windows-x64.zip`, **108529 bytes**.
- SHA256 `c1c3e184e67df75d458ec64c8885eb1c99044a59c0d93d37d73e960417e41dab`.
- Assets: ZIP y `SHA256SUMS.txt`. SHA256 de ambos confirmado contra el digest calculado por GitHub.

El ZIP y las sumas se descargaron desde sus enlaces públicos **sin autenticación**. La descarga coincide con el paquete local revisado. El validador comprobó la lista exacta de cuatro archivos (`AutoNauta.exe`, `LEEME.txt`, `LICENSE.txt`, `SHA256SUMS.txt`), las sumas internas, guía/licencia de la entrega y versión 2.4.3.0. No se incluyeron datos personales, archivos de sesión, configuración, diagnósticos ni perfiles VPN.

GitHub confirmó repositorio público y licencia MIT detectada. Se habilitaron y verificaron secret scanning, protección de pushes y reporte privado de vulnerabilidades; la consulta de alertas abiertas antes de publicar devolvió cero. La revisión local previa abarcó 13 commits y 192 blobs únicos, más el árbol preparado, sin coincidencias con secretos conocidos o patrones seleccionados. Esto no acredita ausencia universal de secretos o fallos.

La revisión de Codex y GPT-6 Luna Max identificó y corrigió ReDoS en HTML malformado; 95 pruebas del núcleo pasaron y la interfaz final se comprobó a 100 %, 150 % y 200 %. También se corrigió trabajo de prioridad innecesario mientras el indicador está oculto o suspendido. Informe y límites: [SEGURIDAD-RELEASE-2.4.3.md](../SEGURIDAD-RELEASE-2.4.3.md).

La entrega local quedó instalada y activa, con respaldo anterior ignorado y conservación de cuenta, contraseña/contexto protegidos, modo, fijación y posición. El ejecutable no está firmado digitalmente y sigue siendo beta.
