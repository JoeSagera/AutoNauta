# AutoNauta 2.4 — recuperación, estadísticas y actividad

13 de octubre de 2026. Versión compilada e instalada: **2.4.0.0**.

## Incidente de las 17:19:39

El usuario reinició Nauta y confirmó que el login automático recuperó la sesión, pero el indicador quedó rojo hasta pulsar «Comprobar ahora». El registro local contiene:

| Hora | Estado y mensaje |
|---|---|
| 17:18:39 | Gris: Verificando caída |
| 17:19:05 | Gris: Esperando portal |
| 17:19:38 | Amarillo: Iniciando sesión |
| 17:19:39 | Rojo: No se pudo continuar; detalle genérico |
| 17:20:03 | Verde: Internet conectado |

El registro anterior no conservaba tipo, código ni fase de la excepción. No se puede determinar retrospectivamente su causa exacta ni afirmar que fue un bloqueo permanente: el intervalo creciente podía retrasar la siguiente comprobación y el clic la adelantó.

Durante las pruebas se reprodujeron errores de escritura de configuración con el mensaje de Windows «Unable to remove the file to be replaced». Es una **causa posible**, no una causa demostrada del incidente original. El reemplazo de configuración podía fallar justo al guardar el nuevo contador después de un login aceptado, interrumpiendo la comprobación de Internet. La [documentación de ReplaceFileW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew) especifica que el código 1175 conserva los nombres de ambos archivos; otros códigos de reemplazo tienen efectos distintos y no se reintentan indiscriminadamente.

## Cambios de recuperación y diagnóstico

- Errores auxiliares al capturar/proteger/guardar el contador no impiden verificar Internet después del login.
- Un error posterior al inicio del intento activa comprobaciones breves de recuperación; no exige un clic manual ni repite de inmediato las credenciales.
- Si Nauta confirma sesión conectada, se conserva esa confirmación antes de leer el contador o verificar la salida pública. Mientras espera Internet, no se descarta el nuevo contexto porque la página principal siga mostrando un formulario.
- Al confirmar Internet se termina esa espera especial. Una caída nueva se vuelve a detectar y contar normalmente; se conserva el intervalo mínimo entre logins y el límite de intentos.
- El diagnóstico registra **fase, tipo de excepción y HResult**. Omite el mensaje original, URL, cuerpo de respuesta y datos privados de sesión. Los errores de certificado y bloqueos explícitos siguen manteniendo sus restricciones.
- El guardado de configuración admite hasta cinco intentos ante códigos transitorios seleccionados, con esperas de 25/50/75/100 ms (250 ms acumulados). No borra el original para sustituirlo ni elimina las restricciones del archivo. Los fallos permanentes se propagan.

## Definición de contadores

La ventana muestra **Esta ejecución**, desde el primer inicio de vigilancia en esta apertura. No se infieren contadores históricos a partir de colores ni se incorpora retrospectivamente el incidente de las 17:19.

| Contador | Cuándo aumenta |
|---|---|
| Caídas | Después de tener Internet, al perder el enlace físico o confirmar dos fallos de conectividad. Una vez por episodio. |
| Recuperaciones | Un episodio contado vuelve a tener Internet comprobado, con o sin login. |
| Intentos AUTO | AutoNauta inicia un intento de login. Incluye intentos que fallen en red o sean rechazados. |
| AUTO efectivos | Nauta confirma el login de ese intento y después se verifica Internet. Una vez por intento confirmado. |

El inicio sin Internet no cuenta como caída. Cambios de color, espera del portal, cambios de ruta o sondas repetidas no multiplican un episodio. Una respuesta «sesión en uso», un rechazo, un POST sin respuesta o vincular una sesión manual no se atribuyen a AUTO efectivo. Una confirmación tardía tras un error de sonda sí puede completar un AUTO previamente aceptado por Nauta.

Pausar/reanudar conserva los números y la hora inicial, pero no inventa caídas durante la pausa ni atribuye una recuperación que pudo suceder fuera de vigilancia. Cerrar y volver a abrir reinicia los contadores; los datos de cuenta y sesión continúan protegidos en disco. Los contadores se mantienen en memoria: no añaden escrituras de configuración periódicas.

## Copia y corrección visual

- **Copiar actividad:** últimas 50 entradas mostradas, con fecha, color/estado, mensaje y detalle completo.
- **Ctrl+C:** filas seleccionadas; **Ctrl+A:** seleccionar todas; **Ctrl+Mayús+C:** toda la actividad.
- Clic derecho para copiar o abrir **Ver registro completo…**, que permite consultar el archivo guardado de aperturas anteriores.
- Doble clic o Intro abre un detalle de solo lectura donde se puede seleccionar y copiar parte del texto.
- Las pruebas sustituyen el destino del portapapeles por una función de captura; no sobrescriben el portapapeles del usuario.

La lista dibujada manualmente ahora invalida toda su superficie al cambiar de tamaño, limpia el ancho actual de cada fila y delimita su dibujo. Evita restos de los textos abreviados al ampliar la ventana. La captura personal adjunta se utilizó solo para inspección y no se publica.

## Validación e instalación

- **92 pruebas correctas, cero fallos**, mediante `Compilar.ps1`. Incluyen episodios, pausas, AUTO confirmado frente a ambiguo, fallos auxiliares del contador, recuperación sin clic después de errores posteriores al POST y de verificación, reintentos de archivo acotados y conservación de configuración original.
- `--ui-smoke`: salida 0, **30 capturas por ejecución** a 100 %, 150 % y 200 %. Incluye acciones reales de selección/copia mediante destino simulado y detalles largos.
- Las imágenes de la lista ampliada antes y después de reducir/ampliar la ventana tienen **el mismo SHA256 en las tres escalas**. Las capturas son de ejemplo, sin cuenta ni sesión reales.
- Instalación final conserva cuenta, contraseña y sesión protegidas, modo, fijación y posición. El ejecutable instalado coincide con el compilado. Copias anteriores y archivos personales permanecen solo en `work/`, fuera de Git.
- **Confirmación manual del usuario:** después de instalar 2.4 se preguntó por los cuatro contadores, Ctrl+C/«Copiar actividad» y redimensionado. Respondió: **«Sí, todo funciona bien»**.

### Consumo medido

Comparación simultánea de 2.3.1 y el candidato 2.4 con el instrumento existente, sin credenciales ni login automático. 20 s de calentamiento y aproximadamente 60 s de medición, con vigilancia real y ventana principal oculta; 16 procesadores lógicos. Ambas terminaron con Internet comprobado. Datos: `outputs/AutoNauta/evidencias-estados-2.4/`.

| Medida | 2.3.1 | Candidato 2.4 |
|---|---:|---:|
| CPU sobre el equipo | 0,034 % | 0,032 % |
| Memoria privada final | 33,41 MiB | 34,25 MiB |
| GDI inicial/final | 51 / 51 | 51 / 51 |
| Objetos de usuario inicial/final | 69 / 69 | 80 / 80 |

El incremento de objetos corresponde a los controles y menú añadidos y permanece estable en la muestra. Esta comparación breve no demuestra estabilidad durante días ni una reducción universal de CPU. La medición precede a ajustes finales de las capturas de prueba y al reinicio de la espera de login al volver a verde; no se usa para medir los casos de error.

## Pendientes

El usuario ya confirmó un login automático real con 2.3.1 durante el incidente, seguido de verde tras el clic manual. La corrección 2.4 recupera sin clic en escenarios controlados; observarla tras otro reinicio real de Nauta sigue pendiente. Esta revisión no provocó un logout ni cambió la VPN. La causa exacta de la excepción antigua sigue sin demostrarse; ahora hay diagnóstico suficiente para distinguir nuevos errores.

Paquete local preparado: `outputs/releases/v2.4.0/AutoNauta-v2.4.0-windows-x64.zip`, 102407 bytes; SHA256 `b746459ea7716530e80214abdcda167391fc0d9c09ab585c8f43bba249886f35`. Contiene solo ejecutable, guía y sumas de comprobación. No se anuncia una nueva Release publicada.
