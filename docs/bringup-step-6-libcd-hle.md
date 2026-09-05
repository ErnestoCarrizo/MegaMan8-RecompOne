# Paso 6: identificación y prueba de HLE para `libcd`

## Objetivo

El paso 5 localizó el bloqueo en la lectura asíncrona del recurso `0x5D`. En este paso se identificaron las funciones PsyQ involucradas para que el recompilador las enlace con `RecompOne.Runtime.Sdk.LibCd`.

## Funciones identificadas

| Dirección | Nombre | Evidencia principal |
| --- | --- | --- |
| `0x800CEE78` | `CdSync` | Wrapper del núcleo de sincronización `0x800CFA18`. |
| `0x800CEE98` | `CdReady` | Wrapper del núcleo de eventos de datos `0x800CFC98`; se usa dentro del callback `DataReady`. |
| `0x800CEEB8` | `CdSyncCallback` | Intercambia el puntero del callback de sincronización en `0x800DA060`. |
| `0x800CEED0` | `CdReadyCallback` | Intercambia el puntero del callback de datos en `0x800DA064`. |
| `0x800CEEE8` | `CdControl` | Recibe comando, parámetros y resultado; se llama con `Setmode`, `Setloc` y `ReadN`. |

La distinción entre `CdSync` y `CdReady` es importante. Ambas tienen la misma firma pública, pero utilizan núcleos distintos. El cargador llama `CdReady(1, result)` desde su callback para consultar o copiar el evento de sector disponible.

## Resultado de la recompilación

Después de renombrar las funciones, RecompOne informó:

```text
Functions from map:       1875
Total generated:          1876
HLE replacements applied: 6
Build errors:             0
```

Las seis sustituciones son `VSync` y las cinco funciones de `libcd` de la tabla anterior.

## Pruebas comparativas

### Cinco HLE de `libcd`

Con el grupo completo, el juego ejecutó la inicialización del lector y el primer `Setmode`, pero se detuvo antes del `Setloc` del recurso `0x5D`.

La causa está en el sondeo previo de `func_800FBA3C`:

```text
CdReady(mode=1, result=null)
repetir mientras el resultado sea distinto de 0
```

En ese punto no existe un evento de datos pendiente, por lo que la implementación original devuelve `0`. La HLE actual conserva `_lastIntr = Complete` y `CdReady` devuelve `2`; como el modo no bloqueante no consume ni distingue ese estado anterior, el bucle nunca termina.

### `CdControl` y callbacks HLE, `CdReady` original

Esta variante de diagnóstico sí llegó a:

```text
[SDK] Cd cmd 0x0E param=0x80155529 pos=00:00:00
[SDK] Cd cmd 0x02 param=0x801554F4 pos=00:00:00
[SDK] Cd cmd 0x06 param=0x00000000 pos=29:12:03
```

Esto confirma que `CdControl` interpreta correctamente `Setmode`, `Setloc` y `ReadN`. Sin embargo, tampoco completa la carga. RecompOne entrega los callbacks de lectura desde `LibCd.Tick()`, llamado por `Runtime.PresentFrame()`. Mega Man 8 espera sin ejecutar otro `VSync`, así que no presenta un nuevo cuadro y `Tick()` no vuelve a ejecutarse.

## Conclusión

Las cinco identificaciones son consistentes y RecompOne las reconoce como HLE. El bloqueo restante no se resuelve renombrando más funciones: el modelo temporal del lector HLE presupone que el juego continúa presentando cuadros o consultando el CD, mientras Mega Man 8 espera callbacks asíncronos dentro de un bucle sin `VSync`.

El siguiente paso debe ser un adaptador pequeño y específico del port que conserve las HLE, pero permita bombear eventos `DataReady` durante esa espera. La opción menos invasiva es reemplazar localmente la llamada `CdControl(ReadN)` por un wrapper que delegue en `LibCd.CdControl` y avance `CdReady` hasta que el estado del cargador en `0x801C335C` deje de ser `1`, con un límite de seguridad. Antes de hacerlo también debe identificarse `CdControlB`, utilizado por la rutina de finalización para enviar `Pause`.
