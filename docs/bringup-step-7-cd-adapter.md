# Paso 7: adaptador asíncrono de CD para Mega Man 8

## Objetivo

El paso 6 demostró que las HLE de `libcd` entienden los comandos del juego, pero que su avance temporal no coincide con la espera usada por Mega Man 8. El objetivo de este paso fue conservar las implementaciones de RecompOne y adaptar solamente el punto donde el cargador espera los callbacks `DataReady`.

## Funciones adicionales identificadas

| Dirección | Nombre | Evidencia principal |
| --- | --- | --- |
| `0x800CF14C` | `CdControlB` | Ejecuta un comando, espera el resultado y devuelve éxito; la finalización lo usa con `Pause` (`0x09`). |
| `0x800CF2B4` | `CdGetSector` | Recibe destino y cantidad de palabras; los callbacks lo usan para copiar hasta `0x200` palabras del sector actual. |
| `0x800FC1E0` | `PollCdLoader_MM8` | Procesa la cola del cargador y devuelve verdadero cuando `0x801C335C` vale `2`. Es la función repetida por las esperas de recursos. |

`CdControlB` y `CdGetSector` se enlazan directamente con las HLE de RecompOne. `PollCdLoader_MM8` tiene semántica propia del juego y se reemplaza por un parche local.

## Primer diseño descartado

La primera versión bombeaba sectores de manera sincrónica dentro de `CdControl(ReadN)`, con un límite de 16.384 iteraciones. La prueba mostró que el callback procesó 20 sectores y luego el juego reinició internamente una lectura. Ese segundo `ReadN` ocurrió mientras el primer callback seguía activo, por lo que la protección contra reentrancia de `LibCd` impidió entregar más datos.

La captura decisiva fue:

```text
state=1 sectors=20/3 bytes=40960 remaining=300320
```

El límite de seguridad evitó un bloqueo infinito y confirmó que aumentar el número de iteraciones no era una solución.

## Diseño final

El archivo `patches/MegaMan8CdPatches.cs` contiene tres adaptaciones:

1. `CdReady` devuelve `0` para el sondeo no bloqueante previo a una lectura, porque Mega Man 8 espera `CdlNoIntr` y la HLE conserva el resultado `Complete` anterior.
2. `CdControl` delega el comando en `LibCd.CdControl` sin iniciar un bucle propio.
3. `PollCdLoader` entrega como máximo un sector mediante `LibCd.CdReady` cada vez que el juego consulta el estado del cargador. Luego conserva el procesamiento de cola de `func_800FC274` y el contrato de retorno original.

El tercer punto es el importante: el bombeo ya no ocurre dentro del callback ni dentro de `CdControl`. Si un callback reinicia la lectura, el nuevo comando termina primero y el próximo sector se entrega en la siguiente iteración de espera.

La recompilación informa ahora tres parches locales y seis sustituciones HLE:

```text
Patches applied:          3
HLE replacements applied: 6
Total generated:          1876
Build errors:             0
```

Las HLE directas son `VSync`, `CdSync`, `CdSyncCallback`, `CdReadyCallback`, `CdControlB` y `CdGetSector`. `CdReady` y `CdControl` conservan nombres con sufijo `_MM8` en el mapa para que el recompilador no sustituya los wrappers propios antes de aplicar los parches.

## Resultado de ejecución

La lectura que comenzó en `29:12:03` terminó con `Pause` en `29:14:18`. Después comenzó una segunda lectura en `28:09:36`, que terminó en `28:10:18`. El monitor observó en el cuadro 2:

```text
cdStreamState: 0 -> 2
cdBytesDone:   0 -> 0x1D000
sceneState:    0 -> 0xFF
```

También cambió la firma de la región `sdkCodeWindow`, señal de que el contenido cargado modificó RAM como esperaba el juego. A partir de ahí se ejecutó un nuevo bucle estable dentro de `func_800F7C6C`; su llamada a `VSync` retorna a `0x800F7CE0`. La prueba final alcanzó más de 2.700 cuadros sin excepciones, destinos desconocidos ni nuevos bloqueos de CD.

## Conclusión y siguiente paso

El bloqueo asíncrono inicial quedó resuelto sin modificar el submódulo RecompOne. La adaptación vive enteramente en el proyecto y afecta sólo al cargador de Mega Man 8.

El siguiente paso recomendado es diagnosticar el estado visual del nuevo bucle: confirmar qué dibuja `func_800F7C6C`, identificar las llamadas de GPU y entrada que necesita esa escena, y comprobar si la pantalla de arranque responde al control. La captura automatizada de ventanas no expuso la ventana nativa del port en esta sesión, así que esa verificación visual queda separada de la validación de ejecución realizada aquí.
