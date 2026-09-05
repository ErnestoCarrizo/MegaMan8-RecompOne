# Paso 5: monitor de arranque y bloqueo de lectura de CD

## Objetivo

El paso anterior dejó un ejecutable que superaba la espera inicial de `VSync`, pero no mostraba todavía un cuadro de juego. Para distinguir entre una espera normal, un overlay faltante y un periférico sin emular, se añadió un monitor de arranque optativo.

El monitor se activa con:

```powershell
.\scripts\run-port.ps1 -BringupTrace
```

También se puede añadir `-SdkLog` para habilitar los mensajes detallados del SDK y del controlador de CD. Ninguna de las dos opciones altera una ejecución normal.

## Qué observa

En cada `VSync`, el monitor registra:

- nuevos llamadores de `VSync`;
- overlays cargados;
- cambios en direcciones de RAM documentadas por las notas de RetroAchievements;
- el estado y los contadores del cargador de CD identificados durante este paso;
- firmas FNV-1a de dos regiones de código para detectar código cargado o reemplazado dinámicamente.

Las direcciones de juego se expresan como offsets físicos de los 2 MiB de RAM de PS1. Por ejemplo, `0x001C335C` corresponde a la dirección MIPS `0x801C335C`.

## Primera captura

La ejecución instrumentada llegó a `ResetGraph`, `CD_init` y un único `VSync`. El único llamador observado fue:

```text
VSync return address: 0x800FB790
frame:                1
overlay activo:       main
```

Los 12 valores de juego todavía estaban en cero y las dos regiones de código conservaron sus firmas iniciales. No hubo un segundo `VSync`, ningún cambio de código ni carga de overlay. Por tanto, el bloqueo ocurre después de devolver desde la inicialización que contiene ese primer `VSync`.

## Traza temporal de funciones

Se generó temporalmente una compilación con la traza completa de funciones. Esta opción se retiró después del diagnóstico y no forma parte de la configuración guardada.

En diez segundos se registraron 324.134 líneas. El patrón dominante fue:

```text
func_800FC1E0: 160575 llamadas
func_800FC274: 160575 llamadas
```

`func_800FC1E0` procesa la cola interna mediante `func_800FC274` y devuelve verdadero únicamente cuando la palabra en `0x801C335C` vale `2`. La dirección de retorno añadida a una segunda captura fue siempre `0x800FD180`; el valor observado permaneció en `1` y la bandera de inhibición en `0`.

El llamador se encuentra dentro de `func_800FD124`. Primero solicita el recurso número `0x5D` mediante `func_800FB9C0` y luego espera en `0x800FD178–0x800FD180` a que termine la carga. Después debería repetir el proceso con el recurso `0x17`.

## Secuencia del CD observada

El cargador instala dos callbacks y emite los comandos equivalentes a `Setloc` y `ReadN`. La captura mostró:

```text
callback de sincronización: interrupción 0x02, 2 llamadas
callback de datos:          interrupción 0x01, 1 llamada
estado del cargador:        0x00000001
```

La interrupción `0x02` indica que los comandos fueron aceptados y completados. La única interrupción `0x01` entrega el primer sector de la lectura. Ese callback comienza con los contadores de sectores y bytes en cero, procesa el sector inicial y espera posteriores eventos de datos; esos eventos nunca llegan. Como consecuencia, la rutina de finalización `func_800FC18C` no se ejecuta y el estado no cambia de `1` a `2`.

## Conclusión

El bloqueo actual no está en `VSync`, en la GPU, en las variables de juego ni en un overlay aún desconocido. Está localizado en la continuación de una lectura asíncrona de CD: el runtime entrega el primer evento `DataReady`, pero no continúa entregando sectores al callback registrado por el juego.

Las funciones del SDK todavía conservan nombres genéricos, por lo que RecompOne sólo aplica la sustitución HLE de `VSync`. El siguiente paso recomendado es identificar y renombrar primero el pequeño grupo de funciones de `libcd` que participa en esta secuencia —`CdSync`, `CdControl`, `CdSyncCallback` y `CdReadyCallback`—, regenerar y verificar si las implementaciones HLE de RecompOne completan la lectura del recurso `0x5D`. Debe hacerse como un cambio aislado para saber exactamente qué sustitución desbloquea el arranque.
