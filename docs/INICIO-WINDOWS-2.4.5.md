# Versión visible e inicio con Windows · 2.4.5

La ventana principal muestra `AutoNauta 2.4.5 beta` en el título. La versión procede de los metadatos del ejecutable, para evitar mantener otro número en la interfaz.

**Iniciar con Windows** está activado por defecto, tanto para una configuración nueva como al abrir una configuración anterior que no tenga esa preferencia. La elección de desactivarlo se guarda y se conserva en las siguientes aperturas. El interruptor está junto a Vigilancia/Login automático y también en el menú del LED.

Se registra únicamente `AutoNauta` en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. El comando utiliza la ruta absoluta entre comillas seguida de `--startup`. Se ejecuta al iniciar sesión con ese usuario de Windows; no es un servicio antes del inicio de sesión. Desactivar la opción elimina esa entrada, sin modificar otros programas.

El arranque abre solo el indicador y activa la vigilancia, conservando AUTO/MAN, cuenta, posición y fijación. No muestra primero la ventana principal para después esconderla. Si ya existe una instancia, un arranque en segundo plano termina con código 0 sin abrir un aviso. La apertura manual conserva la ventana inicial y el aviso de instancia duplicada.

Cuando se mueve la copia portátil y se vuelve a abrir, se actualiza la ruta. El programa debe mantenerse extraído en una carpeta permanente. Salir termina la ejecución actual y conserva la preferencia de inicio. Windows puede deshabilitarla desde el Administrador de tareas independientemente de la preferencia guardada; la aplicación no elimina ese bloqueo. Las vistas previas, pruebas y diagnósticos no registran el ejecutable temporal en el arranque real.

Si falla el acceso al registro se mantiene funcionando la aplicación y la fila muestra la posibilidad de reintentar. Si el cambio del registro se completa pero falla el guardado de preferencias, se restaura el valor anterior del registro, incluido su tipo.

## Comprobaciones del 14 de octubre de 2026

- 101 pruebas del núcleo correctas. Incluyen preferencia por defecto en configuración nueva/anterior, persistencia de desactivación, ruta con espacios, actualización, idempotencia, conservación de entradas ajenas y reversión tras un fallo de guardado.
- Interfaz al 100 %, 150 % y 200 %: salida 0 y 34 capturas por escala. Se inspeccionó la captura de la ventana con la nueva fila. Una ejecución inicial falló en una aserción de foco de la prueba de superposición; la repetición completa pasó sin modificar ese componente. Se conserva el resultado local de las pruebas.
- `tests/StartupUiProbe.cs`: en una vista previa aislada, comprueba ventana principal oculta, indicador visible, versión real, recuperación de la ventana y entrada personal de inicio sin modificaciones.
- Instalación local 2.4.5.0: copia anterior protegida, cuenta, contraseña cifrada, sesión cifrada, modo y posición conservados. La entrada real coincide con la ruta instalada y `--startup`; no había bloqueo de AutoNauta en StartupApproved. Otra ejecución `--startup` terminó con código 0 y quedó una única instancia activa.
- ZIP local validado: cuatro archivos, licencia MIT, versión y SHA256. Esta actualización no publica una nueva Release por sí sola; la última Release pública sigue siendo 2.4.4.

El inicio se comprobó ejecutando el comando de arranque, sin reiniciar Windows. Las pruebas no sustituyen la observación de un próximo inicio de sesión real ni una medición prolongada de recursos.

Para reproducir la prueba aislada de inicio desde PowerShell, en la raíz del repositorio:

```powershell
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskSources = @(Get-ChildItem src -Filter '*.cs' | ForEach-Object FullName)
$taskSources += Join-Path $PWD 'tests\StartupUiProbe.cs'
$taskProbe = Join-Path $env:TEMP ('AutoNautaStartupProbe-' + [guid]::NewGuid().ToString('N') + '.exe')
& $taskCompiler /nologo /platform:x64 /target:exe "/out:$taskProbe" /main:StartupUiProbe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll $taskSources
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar la prueba.' }
& $taskProbe
if ($LASTEXITCODE -ne 0) { throw 'Falló la prueba de inicio.' }
```

Se necesita un escritorio Windows interactivo. CI mantiene las pruebas del núcleo y del paquete.
