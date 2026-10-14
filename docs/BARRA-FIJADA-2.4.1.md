# AutoNauta 2.4.1 — barra fijada informativa

13 de octubre de 2026. Cambio de interfaz en `IndicatorForm`, `src/Ui.cs`; el motor, las estadísticas y la red de 2.4.0 se conservan.

Cuando la barra está fijada y el mouse sale, se ocultan AUTO/MAN y la chincheta. Permanecen LED, tiempo, bajada y subida. El ancho pasa suavemente de 356 a 280 unidades a 96 DPI, eliminando el espacio de los botones. Al volver el mouse se recupera la barra de 356 unidades y los controles vuelven a ser accesibles. Se mantiene la altura de 26 unidades y el tamaño de letra.

Después de confirmar que el ocultamiento funcionaba, el usuario pidió quitar el espacio vacío entre las cifras. Las columnas de tiempo, bajada y subida pasan de 108/88/88 a 90/76/74 unidades. La barra informativa es 44 unidades más estrecha que el primer candidato 2.4.1 de 324; con controles también se reduce 44 unidades frente a 400. Se conservan los tamaños de los botones y los márgenes alrededor de las cifras.

El LED conserva su punto en pantalla al cambiar de ancho, tanto a izquierda como a derecha. Mientras hay un menú o arrastre, los controles permanecen disponibles. La barra fijada sigue mostrando información en todos los colores y no se convierte en muesca por estar verde. Al desfijar, se recupera el comportamiento lateral anterior.

Los controles ocultos no responden a clics y cambiar AUTO/MAN desde otra parte no redibuja un botón invisible. Se reutiliza el temporizador de transición existente, detenido al estabilizarse; no se añade sondeo del mouse ni temporizador permanente. La superficie gráfica expandida se reutiliza.

Validación: `Compilar.ps1` ejecutó **92 pruebas correctas, cero fallos**. Como el ejecutable de entrega estaba abierto tras la pausa, el candidato se compiló con el mismo compilador y argumentos en una carpeta temporal para revisarlo antes de sustituirlo. `--ui-smoke` a 100 %, 150 % y 200 %: salida 0 y **34 capturas** por escala. Incluye barra fijada sin botones, hover con controles, retirada del mouse, clics deshabilitados mientras están ocultos, posición del LED, ausencia de animación/retardo en reposo y ausencia de redibujados por AUTO invisible en ambos laterales. También conserva las comprobaciones de estados, recuperación del shell, copia de actividad y recursos gráficos. Capturas compactas revisadas: `outputs/AutoNauta/barra-fijada-2.4.1/` (escala 150 %, datos ficticios).

El primer candidato 2.4.1.0 se instaló con respaldo local de 2.4.0 y el usuario confirmó el gesto de fijar/salir/volver. La revisión compacta también se instala con respaldo y conserva cuenta, contraseña y sesión protegidas, modo, fijación y posición. Las capturas son de vista previa y datos ficticios; los respaldos personales permanecen fuera de Git. El ancho reducido se comprobó con el modo de pruebas visuales en ambos laterales y las tres escalas; la apreciación del nuevo tamaño por el usuario queda pendiente.

Actualizar reinicia los contadores de esta ejecución, igual que cerrar y volver a abrir AutoNauta. El registro histórico y el contador de horas de la sesión se conservan.

Revisión compacta instalada en `outputs/AutoNauta/AutoNauta.exe`; instancia abierta y copia instalada idéntica al candidato. Paquete local `outputs/releases/v2.4.1/AutoNauta-v2.4.1-windows-x64.zip`, 103027 bytes, SHA256 `5a3818967ee1716df537a2d5b6f4a7bc9c9d9a5790656342afdca2b5175d0e3a`. Contiene únicamente ejecutable, guía y sumas SHA256. Preparar este paquete no publica una Release en GitHub.
