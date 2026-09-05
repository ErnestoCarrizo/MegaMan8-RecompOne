# Paso 8: biblioteca gráfica, planificador y límite de hilos

## Resultado

El bucle estable alcanzado después de las dos lecturas de CD no es todavía el menú del juego. Es el planificador cooperativo de Mega Man 8. La inicialización termina correctamente, crea la tarea principal y presenta cuadros, pero RecompOne no cambia a la tarea creada porque su llamada BIOS `ChangeTh` aún no implementa el cambio de contexto.

Este análisis permitió:

- identificar 51 funciones con nombre comprobable;
- activar ocho sustituciones HLE adicionales de `libgpu`;
- elevar el total de sustituciones HLE automáticas de 6 a 14;
- reconstruir la estructura de cuatro tareas del juego;
- localizar el selector y subestado de escenas;
- identificar el callback por VSync que intercambia buffers, envía la lista de dibujo y actualiza la entrada;
- demostrar por qué el estado estable actual no progresa al menú.

## Símbolos de PsyQ identificados

Los nombres gráficos se comprobaron mediante los textos de diagnóstico que la propia biblioteca PsyQ dejó dentro de `SLUS_004.53`, sus argumentos y los accesos a la GPU. No dependen de una conjetura por proximidad.

| Dirección | Nombre | Tratamiento |
|---|---|---|
| `0x800D37C0` | `ResetGraph` | código original |
| `0x800D3918` | `SetGraphReverse` | código original |
| `0x800D3A2C` | `SetGraphDebug` | código original |
| `0x800D3A98` | `SetGraphQueue` | código original |
| `0x800D3B64` | `DrawSyncCallback` | código original |
| `0x800D3BC0` | `SetDispMask` | código original |
| `0x800D3C5C` | `DrawSync` | HLE `LibGpu.DrawSync` |
| `0x800D3DF0` | `ClearImage` | HLE `LibGpu.ClearImage` |
| `0x800D3E84` | `LoadImage` | HLE `LibGpu.LoadImage` |
| `0x800D3EE8` | `StoreImage` | HLE `LibGpu.StoreImage` |
| `0x800D3F4C` | `MoveImage` | HLE `LibGpu.MoveImage` |
| `0x800D4010` | `ClearOTag` | código original |
| `0x800D40C8` | `ClearOTagR` | código original |
| `0x800D41C0` | `DrawOTag` | HLE `LibGpu.DrawOTag` |
| `0x800D4234` | `PutDrawEnv` | HLE `LibGpu.PutDrawEnv` |
| `0x800D42F8` | `DrawOTagEnv` | código original |
| `0x800D440C` | `PutDispEnv` | HLE `LibGpu.PutDispEnv` |

También se reconocieron estas funciones de soporte por su estructura y por la interfaz estándar que implementan:

| Dirección | Nombre | Evidencia principal |
|---|---|---|
| `0x800CDE68` | `PadInit` | instala el buffer mediante `PAD_init2` y configura el modo de limpieza |
| `0x800CDEB8` | `PadRead` | ejecuta `PAD_dr` y devuelve los bits activos en alto |
| `0x800CDEE8` | `PadStop` | llama a `StopPAD2` |
| `0x800CE0EC` | `ResetCallback` | invoca la entrada correspondiente de la tabla de callbacks |
| `0x800CE17C` | `VSyncCallback` | registra el callback en la ranura cero |
| `0x800D2368` | `SetDefDrawEnv` | construye una estructura `DRAWENV` |
| `0x800D2438` | `SetDefDispEnv` | construye una estructura `DISPENV` |
| `0x800D2474` | `SetDumpFnt` | selecciona un stream de fuente abierto |
| `0x800D24B4` | `FntLoad` | inicializa la fuente en las coordenadas VRAM indicadas |
| `0x800D2558` | `FntOpen` | recibe `x, y, w, h, isbg, maxchars` y devuelve un identificador |
| `0x800D3240` | `SetPolyF3` | fija longitud 4 y código GPU `0x20` de un `POLY_F3` |
| `0x800D696C` | `InitGeom` | habilita COP2 y carga los valores iniciales estándar del GTE |

## Planificador propio de Mega Man 8

`RunTaskScheduler_MM8` (`0x800F7C6C`) mantiene cuatro ranuras de `0x50` bytes:

| Tarea | Dirección física | Pila inicial |
|---|---|---|
| 0 | `0x001FC000` | `0x001FEC00` |
| 1 | `0x001FC050` | `0x001FF000` |
| 2 | `0x001FC0A0` | `0x001FF400` |
| 3 | `0x001FC0F0` | `0x001FF800` |

El puntero a la ranura que el planificador está procesando se guarda en `0x001FC100`. Los estados comprobados son:

| Estado | Significado observado |
|---|---|
| `0` | ranura libre |
| `1` | dormida; el contador de 16 bits en `+2` disminuye por cuadro |
| `2` | preparada; debe crearse su hilo BIOS |
| `4` | reanudación especial |
| `0x10` | hilo creado/activo |

Las operaciones identificadas son:

- `ConfigureTaskSlot_MM8` (`0x800F7E08`): asigna una función de entrada a una ranura y la deja en estado 2.
- `SleepCurrentTask_MM8` (`0x800F7ECC`): guarda el retraso, marca estado 1 y cede el hilo.
- `ResetTaskSlot_MM8` (`0x800F7F58`): cierra el hilo y libera una ranura.
- `ResetAllTaskSlots_MM8` (`0x800F7FC4`): aplica el reset a las cuatro ranuras.
- `ReplaceCurrentTask_MM8` (`0x800F8000`): cierra la ejecución actual, instala una nueva entrada y cede al planificador.
- `RestartBootTask_MM8` (`0x800F834C`): limpia subsistemas y vuelve a instalar la tarea de arranque.

La secuencia por cuadro del planificador es:

1. esperar que termine la GPU con `DrawSync(0)`;
2. presentar/sincronizar con `VSync(0)`;
3. actualizar el detector de reinicio por combinación de botones;
4. recorrer las cuatro ranuras y crear, despertar o cambiar a sus hilos;
5. ejecutar el cierre de cuadro del juego;
6. consultar `VSync(1)` y repetir.

## Tarea de arranque y escenas

`BootTask_MM8` (`0x800F7B8C`) prepara ajustes, limpia estado de escena y finalmente reemplaza su propia entrada por `DispatchSceneTask_MM8` (`0x800FEA40`). El dispatcher ejecuta una función de escena, duerme un cuadro y vuelve a repetir.

El selector principal es el byte `0x001CF840`; el subestado es `0x001CF844`. Su tabla principal, comprobada directamente en el ejecutable legal, contiene:

| Selector | Función |
|---|---|
| 0 | `0x800FEB10` (`EnterPrimaryScene_MM8`) |
| 1 | `0x800FEB44` |
| 2 | `0x800FF1FC` |
| 3 | `0x800FF5C0` |

La escena 1 usa cinco subestados en `0x80137930`: `0x800FEBEC`, `0x800FED58`, `0x800FEF94`, `0x800FF0F4` y `0x800FF148`. La escena 2 usa dos subestados en `0x80137944`: `0x800FF238` y `0x800FF47C`.

Todavía no se asignaron nombres como “logo”, “título” o “demo” a esas entradas. La estructura de dispatch está demostrada, pero la semántica visual necesita una ejecución que realmente haga correr la tarea.

## Callback de cuadro e input

`FrameVSyncCallback_MM8` (`0x800F84E8`) es el trabajo central de cada VSync:

- incrementa el contador global de cuadros;
- aplica `PutDispEnv` y `PutDrawEnv` al buffer activo;
- sube transferencias pendientes a VRAM;
- envía la ordering table mediante `DrawOTag`;
- alterna los dos buffers de dibujo;
- limpia la ordering table siguiente;
- lee ambos controles y calcula estados actuales y pulsaciones nuevas.

Los bits de pulsación nueva del primer control quedan en `0x001B2958`. `UpdateResetChord_MM8` (`0x800F8864`) exige que la máscara `0x0900` se mantenga durante `0x79` cuadros, siempre que no haya una lectura de CD activa, antes de reiniciar la tarea de arranque. El propósito de “reinicio por combinación” es firme; los nombres físicos exactos de los dos botones se dejaron pendientes hasta validar el orden de bits que entrega esta versión de la rutina de pad.

`GpuDrawSyncCallback_MM8` (`0x800F86A8`) consulta `VSync(1)` y conserva el resultado en `0x001D28F4`.

## Por qué hizo falta el adaptador de CD

En una PlayStation real, `CdControl(CdlReadN, ...)` inicia una operación asíncrona. Después de que la función retorna, el controlador de CD y sus interrupciones continúan entregando sectores aunque el código del juego se encuentre consultando un estado en un bucle.

RecompOne modela correctamente la lectura por sectores, pero normalmente la hace avanzar desde `Runtime.PresentFrame()`. Mega Man 8 inicia `ReadN` y espera dentro de `PollCdLoader_MM8` sin llamar a otro `VSync`. Se producía entonces una dependencia circular:

1. el juego esperaba que llegaran más sectores;
2. RecompOne esperaba el siguiente cuadro para entregar esos sectores;
3. el juego no presentaba ese cuadro hasta terminar la lectura.

Había además una diferencia de contrato: después de un comando anterior, el HLE conservaba `Complete` como última interrupción. La consulta no bloqueante previa de Mega Man 8 espera `CdlNoIntr` cuando no existe una lectura asíncrona activa, por lo que podía entrar en otro bucle antes de iniciar `ReadN`.

El primer prototipo intentó entregar todos los sectores dentro de `CdControl`. No era correcto: al completar un bloque, el callback del juego inicia el siguiente `ReadN` mientras el primer bombeo aún está activo. La protección contra reentrada de `LibCd` impedía que esa lectura anidada produjera callbacks.

El adaptador final mantiene la asincronía y hace tres cosas delimitadas a este juego:

- `CdReady_MM8` devuelve `CdlNoIntr` sólo para la consulta no bloqueante previa cuando el cargador no está leyendo;
- `CdControl_MM8` delega el comando al HLE sin forzar una lectura sincrónica;
- `PollCdLoader_MM8` entrega como máximo un sector por iteración de espera, fuera de `CdControl`, y luego ejecuta el trabajador original de la cola.

Se implementó como parche local porque la discrepancia combina el modelo temporal de RecompOne con la forma específica en que Mega Man 8 espera sus recursos. Modificar globalmente `LibCd` podría alterar otros juegos; modificar el submódulo también habría mezclado una adaptación del port con el runtime compartido.

## Nuevo límite comprobado

En la traza final, las dos cargas iniciales completan, la tarea 0 pasa de estado `0` a `2` y luego a `0x10`, pero su función de entrada no se ejecuta. `sceneIndex` y `sceneSubstate` permanecen en cero durante más de 900 cuadros.

La causa está en el runtime: `BiosB.OpenTh` reserva un identificador, pero sólo conserva el indicador `Used`; no guarda PC, SP ni GP. La entrada BIOS `ChangeTh` no realiza ninguna operación. Por tanto, el planificador de Mega Man 8 cree que cedió el control a la tarea, mientras el host continúa inmediatamente en el propio planificador.

No conviene sustituir `ChangeTh` por una llamada directa a la función de entrada. Muchas tareas contienen bucles y esperan reanudarse exactamente después de `SleepCurrentTask_MM8`; una llamada directa no preservaría esa continuación y podría bloquear el host o reiniciar la tarea desde su principio. El siguiente adaptador tendrá que preservar contexto/continuación o transformar explícitamente estas tareas en máquinas de estado.

## Validación

- Recompilación: 3 parches propios y 14 sustituciones HLE aplicadas.
- Compilación .NET 10: 0 errores; permanecen las 16 advertencias conocidas de código inaccesible generado.
- Ejecución: más de 900 cuadros en `0x800F7CE0`, sin excepción ni destino de dispatch desconocido.
- CD: estado `2`, `0x1D000` bytes procesados y estado de inicialización `0xFF` conservados.
- Tareas: tarea 0 observada en transición `0 → 2 → 0x10`; tareas 1–3 permanecen libres.
