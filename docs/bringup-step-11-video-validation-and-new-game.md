# Paso 11: validación visual del vídeo y entrada a una partida nueva

## Validación del síntoma informado

Se añadió una captura opcional de la salida visible de VRAM para comprobar la ruta STR de extremo a extremo. La prueba produjo cuadros BMP válidos de 320×240 y 24 bits durante las presentaciones. La inspección mostró correctamente:

- el logo de Capcom;
- cuadros intermedios de la animación de apertura;
- el logo de Mega Man 8;
- la cinemática posterior a elegir una partida nueva.

Esto completa la validación que faltaba en el paso 10: además de leer sectores, decodificar MDEC y transferir por DMA, los píxeles llegan con colores y orientación correctos a la VRAM visible.

La opción de diagnóstico es:

```powershell
.\scripts\run-port.ps1 -SkipBuild -VideoSnapshots
```

Las imágenes se guardan en `logs/video-snapshots/`, separadas por el nombre del overlay activo. El capturador también registra si la GPU se encuentra en RGB24.

### Límite de estas capturas

La sombra de VRAM refleja cargas de imagen y, por tanto, resulta adecuada para comprobar películas STR. El backend gráfico acelerado dibuja los polígonos y sprites normales directamente en su destino de render; ese resultado no se rasteriza de vuelta en la sombra de VRAM. Por esa razón, una captura de este diagnóstico puede conservar datos antiguos o parecer corrupta durante el título o una fase aunque la ventana del port esté dibujando sus sprites correctamente. No debe utilizarse como referencia visual para escenas normales.

## Restauración del modo de pantalla

Las películas trabajan en RGB24, mientras que el título y las fases usan el framebuffer normal RGB15. Se añadió una adaptación pequeña al cambio de overlay que:

1. limpia el indicador `isrgb24` de los dos `DISPENV` del juego (`0x001CF458` y `0x001CF4F8`);
2. reaplica el `DISPENV` correspondiente al buffer que esté activo;
3. mantiene esa corrección durante los primeros 120 cuadros del título o de una demo/fase, sin impedir que una película posterior vuelva legítimamente a RGB24.

No se fuerza permanentemente el modo gráfico. En la prueba, las capturas pasaron a `rgb24=False` al cargar el título y volvieron a `rgb24=True` cuando comenzó la cinemática de la partida nueva.

## Piloto de entrada automática

`-AutoProgress` permite repetir el avance desde el título sin depender del teclado:

```powershell
.\scripts\run-port.ps1 -SkipBuild -Quiet -BringupTrace -AutoProgress
```

La primera versión modificaba la lectura activa-en-cero de la BIOS. Eso demostró que los bits llegaban al juego, pero Mega Man 8 actualiza su estado de control más de una vez entre ciertos `VSync`; una segunda actualización podía borrar el flanco `just pressed` antes de que lo consumiera el menú.

La versión definitiva fija el flanco después de la actualización del pad, en las variables ya identificadas:

| Dirección | Uso | Valor probado |
| --- | --- | --- |
| `0x001B2954` | botones mantenidos del control 1 | máscara del botón |
| `0x001B2958` | botones recién pulsados del control 1 | máscara del botón |
| — | Start | `0x0800` |
| — | Cross | `0x0040` |

La secuencia deja tiempo para terminar la carga del título y después pulsa Start y Cross. Es sólo un instrumento de prueba, desactivado en una ejecución normal.

## Recorridos comprobados

### Atracción automática

Sin seleccionar una partida, el título agotó su temporizador, cargó `demo_stage_1`, completó esa demostración, volvió a recorrer el título y cargó `demo_stage_2`. No hubo excepciones ni llamadas a direcciones sin recompilar.

### Partida nueva

Con el piloto activado se observó esta secuencia:

1. `main` cargó `title` en el cuadro 3228;
2. el estado del título pasó de 1 a 2 al recibir Start;
3. Cross lo llevó de 2 a 3 y eligió la ruta de escena 4;
4. comenzó una cinemática STR completa;
5. aproximadamente 5.246 cuadros después —unos 87 segundos a 60 Hz— se inicializaron dos vidas;
6. se cargó el binario actualmente llamado `demo_stage_1` y permaneció estable durante más de 6.700 cuadros adicionales.

El último dato demuestra que ese overlay no es exclusivo del modo de demostración: la ruta de partida nueva reutiliza el mismo bloque de código. Su nombre actual debe considerarse provisional hasta separar con más precisión el overlay de su modo de ejecución.

## Interpretación de la aparente caída

La secuencia que parecía detenerse en una demo o video promocional era una película larga cuya imagen no se veía antes de restaurar `libcd` streaming. Con la ruta STR habilitada, la película se reproduce y finaliza; el port continúa hasta el siguiente overlay jugable. La prueba se detuvo manualmente, no por una excepción.

## Próximo límite

El siguiente paso es controlar el primer overlay jugable en vez de dejar al personaje inmóvil. Conviene registrar la transición posterior a esa introducción, determinar si conduce al selector de fase y renombrar los overlays con nombres neutrales cuando se confirme cuáles se comparten entre partida y modo de demostración.
