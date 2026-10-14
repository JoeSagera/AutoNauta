# Publicación verificada — AutoNauta 2.4.4 beta

13 de octubre de 2026.

- [Release pública 2.4.4](https://github.com/JoeSagera/AutoNauta/releases/tag/v2.4.4), no borrador, marcada beta/prerelease; se conserva la Release 2.4.3 anterior.
- Tag anotado `v2.4.4`, commit revisado `01eb04254ee5823a3f16a5b54f5e35eba5cbc7d9`.
- [Compilación y validación de GitHub](https://github.com/JoeSagera/AutoNauta/actions/runs/38504682065): **success** para ese commit.
- Paquete `AutoNauta-v2.4.4-windows-x64.zip`, **110144 bytes**.
- SHA256 `a06f24d5d8a6b7f8a9d67d9a2c930effeec8b740fc225d705efa60d7ca406449`.
- Assets: ZIP y `SHA256SUMS.txt`; digests de ambos confirmados contra GitHub y descarga sin autenticación.

Se descargaron el paquete y las sumas desde enlaces públicos. `Validar-Paquete.ps1` confirmó contenido exacto (ejecutable, guía, MIT y sumas internas), igualdad con la entrega local revisada, versión 2.4.4.0 y sumas externas. No se incluyeron datos personales.

La segunda revisión de GPT-6.1 Sol Max confirmó dos P2 y un P3, corregidos en esta versión. Sol verificó los casos con harness separados y ejecutó 98/98 pruebas corregidas; Codex ejecutó también 98/98 y la interfaz a tres escalas. Informe: [REVISION-SOL61-2.4.4.md](../REVISION-SOL61-2.4.4.md). No se afirmó una certificación ni ausencia universal de fallos.

La revisión previa a este commit abarcó 16 commits y 221 blobs únicos, más el árbol preparado, sin coincidencias con datos privados conocidos/patrones seleccionados. Configuración, respaldos y harness personales siguen ignorados. 2.4.4 quedó instalada y activa, con contraseña/contexto protegidos, cuenta, modo, fijación y posición conservados.
