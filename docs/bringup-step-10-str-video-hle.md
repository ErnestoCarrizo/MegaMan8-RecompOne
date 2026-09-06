# Paso 10: vídeo STR del arranque mediante HLE

## Síntoma observado

El juego reproducía el audio XA de las dos secuencias anteriores al título, pero la imagen quedaba negra. Antes de este paso, un registro de DMA/MDEC mostraba únicamente dos transferencias pequeñas al canal de entrada del MDEC: eran las tablas de cuantización y escala. No había datos comprimidos de cuadros, decodificaciones ni transferencias de salida.

La lectura del disco sí comenzaba. Las películas usan `ReadS` desde `00:09:51` y `00:25:11`, pero el lector STR de RecompOne no estaba activo. El audio XA y el vídeo comparten sectores Mode 2, aunque siguen rutas distintas en el runtime; por eso era posible escuchar la secuencia aun cuando ningún cuadro llegaba al MDEC.

## Identificación de `libcd` streaming

El ejecutable contiene las ocho entradas públicas de la biblioteca de streaming. Sus cuerpos, argumentos, estructura de anillo y llamadas entre sí permiten identificarlas sin depender de una coincidencia sólo por posición:

| Dirección | Nombre identificado | Evidencia principal |
| --- | --- | --- |
| `0x800CEBF4` | `StSetRing` | Guarda base y 28 entradas de anillo; después lo limpia. |
| `0x800D123C` | `StClearRing` | Reinicia índices, estados y cabeceras del anillo. |
| `0x800D129C` | `StUnSetRing` | Desconecta callbacks de CD dentro de sección crítica. |
| `0x800D1384` | `StGetBackloc` | Recupera la posición asociada a una cabecera STR. |
| `0x800D13E4` | `StSetStream` | Configura modo, límites y dos callbacks de la secuencia. |
| `0x800D146C` | `StFreeRing` | Convierte el puntero de datos en índice y libera sus entradas. |
| `0x800D1568` | `StGetNext` | Devuelve por referencia el próximo bloque y su cabecera. |
| `0x800D162C` | `StSetMask` | Guarda los tres parámetros de selección de sectores. |

Al asignar estos nombres en `config/funcmaps/main.json`, RecompOne los reconoce automáticamente y sustituye sus cuerpos por `RecompOne.Runtime.Sdk.LibCdStream`. El número de reimplementaciones HLE aumenta de 13 a 21. No hace falta un parche especial de vídeo para Mega Man 8: faltaba enlazar la implementación que ya existía en RecompOne.

## Flujo restaurado

Después de la sustitución, el arranque sigue esta ruta:

1. `StSetRing` activa un anillo de 28 entradas en `0x80010000`.
2. `StSetStream` prepara una película y `ReadS` fija su LBA inicial.
3. El lector STR ensambla los sectores de cada cuadro y `StGetNext` entrega el bloque al juego.
4. Las rutinas originales de Mega Man 8 envían el bitstream al MDEC por DMA 0.
5. El MDEC produce una imagen de 320×240; DMA 1 la copia a `0x80068000` en 20 franjas.
6. La rutina de película llama a `LoadImage`, ya sustituida por HLE, para subir esas franjas a VRAM.

Esta cadena llega hasta la presentación: no se limita a reconocer cabeceras de vídeo.

## Instrumentación añadida

`scripts/run-port.ps1 -VideoLog` activa sólo los registros de DMA y MDEC. Puede combinarse con `-SdkLog` para incluir comandos de CD y llamadas `St*`:

```powershell
.\scripts\run-port.ps1 -SkipBuild -VideoLog -SdkLog
```

El monitor `-BringupTrace` ahora incluye tiempo real, frecuencia del último intervalo y promedio desde el arranque. Esto evita confundir una espera de disco o una secuencia ausente con una frecuencia incorrecta.

## Resultado medido

La prueba instrumentada de ambas secuencias produjo:

- dos inicializaciones completas de `StSetRing`/`StSetStream`;
- 613 cuadros decodificados por MDEC antes de detener manualmente la ejecución;
- 617 transferencias DMA 0, incluyendo cuatro cargas de tablas y los cuadros;
- 12.260 transferencias DMA 1, equivalentes a 20 franjas por cuadro;
- salida MDEC constante de 57.600 palabras por cuadro, correspondiente a 320×240 en 24 bits;
- cero excepciones y cero errores de ejecución.

En los intervalos estables, `VSync` midió entre 59,0 y 60,0 Hz. No se aplicó una reducción de velocidad: el reloj NTSC ya funciona a la cadencia prevista. Las variaciones menores aparecen mientras el host decodifica y transfiere cada cuadro.

## Límite de la verificación

La telemetría demuestra lectura, reconstrucción, decodificación, transferencia a RAM y llamada de subida a VRAM. La apariencia final —colores, recorte, orden de franjas y sincronía audiovisual— debe confirmarse mirando la ventana del port. Si aún hay imagen negra o corrupta, el siguiente punto de observación ya no es el CD: será `LoadImage` y el área visible de VRAM.

