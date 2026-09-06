# Paso 9: tareas cooperativas, título y demostraciones

## Resultado

El port dejó atrás el planificador detenido del paso 8. Ahora alcanza la pantalla de título, deja vencer su espera, ejecuta dos demostraciones completas, regresa al título después de cada una y reconoce el comienzo de una tercera demostración. La observación visual del título se corroboró con el estado interno y con las direcciones documentadas durante el desarrollo de los logros de RetroAchievements.

Para conseguirlo se añadieron dos piezas que RecompOne todavía no ofrece con la semántica que Mega Man 8 necesita:

- un adaptador de los cuatro hilos cooperativos de PsyQ;
- un límite de una entrega de sector de CD por sincronización de video.

También se declararon cuatro overlays dinámicos: título y tres variantes usadas por las demostraciones automáticas.

## Por qué fue necesario el adaptador de hilos

En la PlayStation, `OpenTh` crea un contexto con PC, SP y GP, y `ChangeTh` suspende el hilo actual y continúa otro exactamente desde donde había cedido. Mega Man 8 construye sobre eso un planificador propio de cuatro tareas. La implementación actual de RecompOne reserva el identificador de `OpenTh`, pero no conserva los registros ni cambia de continuación en `ChangeTh`.

Una llamada directa a la función de la tarea no sirve: las funciones recompiladas contienen bucles de larga duración y esperan volver después de `SleepCurrentTask_MM8`, no comenzar desde el principio en cada cuadro. El adaptador mantiene cada pila de llamadas C# en un hilo administrado en segundo plano. Cada tarea espera en una compuerta y `ChangeTh` abre la del destino antes de dormir la del origen. Así se conserva naturalmente la continuación que el juego esperaba de la BIOS.

Los identificadores locales usan `0xFF000000` para la raíz y las ranuras `1` a `3` para las tareas secundarias. El total coincide con `TCB=4` de `SYSTEM.CNF`. `CloseTh` despierta y cierra la tarea indicada sin reutilizar una continuación antigua.

## Afinidad gráfica y contexto de CPU

OpenGL no permite presentar la misma ventana desde un hilo arbitrario. El primer prototipo llamó `VSync` desde una tarea secundaria y el controlador WGL rechazó el contexto gráfico como recurso ya utilizado. Por eso el parche de `VSync` envía la presentación a una cola atendida por el hilo raíz.

Durante esa presentación, los callbacks de CD deben ver los registros de la tarea que pidió el cuadro. El adaptador cambia temporalmente el `CpuContext` activo del runtime por el de esa tarea y restaura el contexto raíz al terminar. Sin ese cambio, un callback podía escribir sus resultados en los registros del planificador y corromper el siguiente cambio de tarea.

## Por qué también se reguló el CD

El runtime puede vaciar hasta 400.000 callbacks `DataReady` en un solo `Tick`. Mega Man 8 usa una cola productor/consumidor acotada: el callback coloca un sector y una tarea del juego debe consumirlo antes de recibir demasiados más. Al entregar 49 sectores dentro del mismo cuadro, la cola se llenaba y el callback quedaba esperando una tarea que todavía no podía ejecutarse.

El `VSync` adaptado desconecta momentáneamente el callback, presenta el cuadro, lo restaura y entrega exactamente un sector. Las lecturas que ocurren antes del bucle de cuadros conservan el parche anterior de `PollCdLoader_MM8`, que también bombea un sector por iteración. Esto mantiene el orden asíncrono y evita modificar el runtime compartido para todos los juegos.

## Overlays comprobados

Los cuatro bloques se cargan en `0x801D8000`; el dispatcher distingue cuál está realmente presente mediante las firmas generadas por RecompOne. Los tamaños no se estimaron por código aparente: se compararon sectores de 2048 bytes de la pista Mode 2 con la RAM capturada y sólo se incluyó la secuencia contigua que coincidía por completo.

| Overlay | LBA inicial | Sectores | Tamaño | Entrada comprobada |
|---|---:|---:|---:|---|
| `title` | 126122 | 9 | 18.432 bytes | `0x801D80A8` |
| `demo_stage_1` | 126131 | 22 | 45.056 bytes | `0x801D81B4` |
| `demo_stage_2` | 126153 | 31 | 63.488 bytes | `0x801D802C` |
| `demo_stage_3` | 126184 | 34 | 69.632 bytes | `0x801D8054` |

La primera definición de `demo_stage_1` tenía sólo siete sectores. La demo llegó aun así hasta un combate y bajó `bossLife` de 12 a 0, pero al terminar llamó a `0x801DC84C`, que quedaba fuera del intervalo recompilado. Los bytes de esa función coincidieron exactamente con el sector 126140: era el mismo overlay, no uno nuevo. La comparación de toda la región reveló los 22 sectores correctos.

Después de ampliar ese bloque, el juego completó la primera demo, regresó al título, cargó `demo_stage_2`, la completó y volvió otra vez al título. La siguiente llamada desconocida, `0x801D8054`, permitió identificar `demo_stage_3` en el sector inmediatamente posterior al segundo bloque.

## Cobertura conseguida

La configuración recompila el ejecutable principal, el título y tres overlays de demostración. Las pruebas demostraron:

- creación, suspensión, reanudación, cierre y reutilización de las tareas;
- presentación gráfica desde tareas sin violar la afinidad de OpenGL;
- lecturas de CD extensas sin saturar la cola del juego;
- selección correcta entre overlays que se superponen en la misma RAM;
- movimiento real del personaje, saltos y actualización de vida de jefe durante las demos;
- retorno de una demostración al título y comienzo de la siguiente.

## Límites pendientes

La tercera demostración fue identificada y recompilada, pero todavía no se observó hasta su final en una prueba completa. Tampoco se validó aún una partida iniciada manualmente, la selección de fase, todas las variantes de nivel, audio XA, guardado ni finalización del juego.

La ejecución se percibe acelerada. El parche conserva el progreso y evita la avalancha de callbacks, pero todavía puede haber más de una llamada de `VSync` del juego por actualización visible y no existe un regulador que reproduzca con precisión la cadencia NTSC de la consola. Este punto debe medirse y corregirse antes de evaluar sincronización audiovisual o física cuadro a cuadro.

Las advertencias `CS0162` provienen de ramas inaccesibles en código generado y no impiden compilar. El barrido lineal y el escaneo de punteros siguen siendo heurísticos: cada ruta nueva debe comprobarse durante ejecución y compararse con los bytes originales cuando aparezca una llamada no mapeada.

## Validación final

- Recompilación: 2.857 funciones totales; 1.876 del ejecutable principal, 100 del título, 238 de la primera demo, 339 de la segunda y 304 de la tercera.
- Sustituciones: siete parches propios y 13 reimplementaciones HLE automáticas.
- Compilación .NET 10: cero errores; 286 advertencias `CS0162` en código generado.
- Ejecución larga: primera y segunda demos completas, con retorno al título; la tercera se detectó por su llamada inicial y su contenido se verificó contra 34 sectores del BIN.
- Arranque final: el dispatcher volvió a reconocer `main` y `title` después del renombrado de overlays.
- Higiene del repositorio: CUE/BIN, volcados de RAM, registros y código recompilado permanecen excluidos de Git.

## Próximo paso recomendado

La siguiente prueba debe iniciar una partida de forma manual y registrar qué overlay carga el selector de fase. En paralelo conviene añadir un reloj de presentación basado en la frecuencia NTSC de PS1 para eliminar la aceleración sin cambiar el número de `VSync` que observa el juego. A partir de ahí, el mismo procedimiento de captura, coincidencia con el BIN y declaración de overlay puede repetirse fase por fase.
