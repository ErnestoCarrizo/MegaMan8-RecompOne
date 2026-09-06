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

La comprobación visual en una GeForce GTX 1050 Ti reveló además que los cuadros
MDEC escritos desde la tarea cooperativa actualizaban la sombra de VRAM de CPU,
pero no la textura OpenGL del backend acelerado: esa tarea no posee el contexto
gráfico de la ventana. Mientras la GPU está en RGB24, el parche desactiva
temporalmente la presentación HLE para que la ventana lea directamente la VRAM de
CPU. Al regresar a RGB15 reactiva el backend acelerado. Con este cambio se ven
correctamente tanto el logo de Capcom como la película de apertura.

La primera medición temporal descubrió que el logo y la apertura llegaban al
título en aproximadamente 61 segundos, mientras que la referencia completa
tarda alrededor de 1 minuto y 42 segundos. `LibCdStream` esperaba tener dos
cuadros simultáneos en su cola antes de activar el reloj. Mega Man 8 consume el
primero antes de que el segundo sea producido, de modo que la profundidad de la
cola nunca alcanzaba dos y el flujo podía avanzar a la velocidad de
decodificación del equipo.

El parche `runtime-patches/0001-fix-str-stream-priming.patch` cuenta cuadros
producidos aunque el consumidor ya los haya retirado. Al segundo cuadro activa
la temporización de sectores. La pista contiene 14.587 sectores entre ambas
películas; a 150 sectores por segundo representan unos 97,25 segundos de flujo,
más las transiciones. El script de compilación aplica este parche de forma
automática e idempotente sobre el submódulo.

La reproducción todavía terminaba antes de tiempo después de ese arreglo: la
apertura se cerraba en el cuadro STR 712, unos 47,40 segundos después de empezar.
La instrumentación comparó el evento de presentación del host con el contador
del callback `FrameVSyncCallback_MM8` en `0x0016D298`:

| Secuencia | VSync del host | VSync contados por el juego |
| --- | ---: | ---: |
| Logo de Capcom | 321 | 638 |
| Apertura | 2.820 | 5.548 |

El reproductor calcula su límite como `(fotogramas_objetivo + 45) * 4`. Para la
apertura, `(1.341 + 45) * 4 = 5.544`; el contador duplicado llegaba a 5.548 justo
cuando sólo se había mostrado el cuadro 712. Esto explica tanto el corte abrupto
como la falta de tiempo para completar el fundido del logo.

La causa estaba en dos fuentes para la misma interrupción: el reloj interno de
`Interrupts` genera VBlank a 60 Hz y `Runtime.PresentFrame()` también encola
IRQ0. El primer arreglo experimental retiró la segunda fuente globalmente. Eso
permitió completar la película, pero durante el título la interrupción quedaba
ligada a puntos de sondeo irregulares del código generado y la animación avanzaba
a tirones.

La versión final de `runtime-patches/0002-avoid-duplicate-vblank.patch` añade el
modo optativo `PresentDrivenVBlank`. Mega Man 8 lo activa desde su adaptación de
`VSync`: el reloj monotónico sigue gobernando `VBlankCount` y las esperas a 60
Hz, pero sólo la presentación encola IRQ0. Así el callback ocurre una vez y en un
punto estable de cada cuadro, sin modificar el comportamiento predeterminado de
otros ports.

Una ejecución completa midió una relación exacta: 402 presentaciones frente a
402 callbacks durante el logo y 5.300 frente a 5.300 durante la apertura. La
película llegó al título y éste continuó normalmente hasta cargar el primer modo
de demostración. La pista contiene fotogramas numerados hasta 1.343, por lo que
terminar en el límite de 1.341 coincide con la tabla original y ya no con el
temporizador adelantado.

Una segunda ejecución con capturas confirmó además el fundido del logo: entre
las presentaciones 240 y 360 el fondo progresa de gris oscuro a gris claro antes
de cambiar a la apertura, en lugar de desaparecer abruptamente.

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
