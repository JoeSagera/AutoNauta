# Desarrollo y comprobaciones

AutoNauta usa C# y Windows Forms sobre .NET Framework 4.8, x64, sin NuGet ni bibliotecas externas. El código está bajo MIT. Ejecuta las comprobaciones con datos ficticios; no fuerces reintentos contra una cuenta real.

## Compilar

Desde la raíz del repositorio en PowerShell, con AutoNauta cerrado:

```powershell
./Compilar.ps1
./Preparar-Release.ps1
./Validar-Paquete.ps1
```

El compilador es el `csc.exe` de .NET Framework x64 instalado con Windows; el script descubre los archivos de `src/`, ejecuta las pruebas antes de sustituir la entrega y produce `outputs/AutoNauta/AutoNauta.exe`. Los ZIP quedan en `outputs/releases`, fuera de Git. El paquete incluye ejecutable, guía, licencia MIT y sumas SHA256. Preparar un ZIP no publica una Release.

## Arquitectura

| Componente | Responsabilidad |
| --- | --- |
| `Engine` / `EngineNetwork` | Ciclo de vigilancia, cancelación, estados y servicios sustituibles en pruebas |
| `Core` / `Network` / `PinnedHttp` | Formulario Nauta, allowlists, rutas por socket, TLS y HTTP acotado |
| `BoundedInput` | Lectura limitada de HTML y regex con timeout |
| `AccountTime` | Contexto de sesión y consulta informativa del tiempo, sin ejecutar JavaScript |
| `Storage` | DPAPI, configuración y registros rotados |
| `RateMonitor` / `ServiceStatistics` | Tráfico del equipo y episodios de caída/recuperación |
| `Ui` / `UiMain` / `UiTheme` | Ventana, actividad copiable, cápsula y muesca lateral |
| `OverlayOrderGuard` | Prioridad del indicador por eventos de ventanas sin cambiar el foco |

El nombre `PinnedHttp` es histórico: aplica validación TLS de Windows con nombre DNS, no pinning de una huella concreta. Las sondas públicas no reciben credenciales. Lee [SECURITY.md](../SECURITY.md) antes de cambiar endpoints, TLS, importación o registros.

## Interfaz sin datos personales

```powershell
$env:AUTONAUTA_UI_SCALE = '1.5'
./outputs/AutoNauta/AutoNauta.exe --ui-smoke ./work/pruebas-visuales
Remove-Item Env:\AUTONAUTA_UI_SCALE
```

Repite a `1`, `1.5` y `2` si cambia el diseño. `--ui-smoke` crea una cuenta y contexto ficticios separados y no consulta la red. Requiere un escritorio Windows interactivo; CI ejecuta las pruebas del núcleo y del empaquetado, no sustituye esta comprobación.

`tests/OverlayOrderProbe.cs`, compilado junto a `src/OverlayOrderGuard.cs` con referencias Windows Forms y Drawing, comprueba avisos reales entre dos procesos y conservación del foco. No demuestra por sí solo compatibilidad con juegos.

## Diagnóstico y publicación

`Diagnosticar.ps1` requiere cerrar la aplicación y hace lecturas de red sin enviar login. Su resultado puede contener IPs, interfaces y metadatos privados: revisa y anonimiza antes de compartirlo.

Antes de una Release, actualiza versión de `AssemblyInfo.cs`, guía, portada y notas; ejecuta núcleo, revisión visual que corresponda y validación del paquete. Revisa árbol e historial para detectar secretos. Usa un tag que apunte al commit comprobado; publica como beta y descarga el asset sin autenticación para comparar su SHA256. Conserva evidencia sin datos personales en `docs/releases`.
