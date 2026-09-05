# Paso 3: reemplazo HLE de `VSync`

## Cambio

La entrada del mapa ubicada en `0x800CDF08`, de 328 bytes, pasó de llamarse `func_800CDF08` a `VSync`. RecompOne reconoce ese nombre como una función de PsyQ y sustituye el cuerpo MIPS por `RecompOne.Runtime.Sdk.LibEtc.VSync`.

## Verificación de generación

Antes del cambio, el recompilador informaba:

```text
[Recompiler] it was applied 0 reimplementations
```

Después del cambio informó exactamente:

```text
[Recompiler] it was applied 1 reimplementations
```

La salida generada contiene un único puente HLE:

```csharp
public static void VSync(CpuContext c, IMemory m) => RecompOne.Runtime.Sdk.LibEtc.VSync(c, m);
```

Las llamadas MIPS que apuntaban a `0x800CDF08` ahora invocan ese puente. El código generado pasó de 11.652.299 a 11.642.097 bytes porque ya no incluye la traducción completa de los 328 bytes originales.

## Verificación de compilación y ejecución

La compilación Release terminó con cero errores y las 16 advertencias ya conocidas de código generado inaccesible. Una ejecución de 15 segundos inicializó OpenGL, cargó `main`, ejecutó `ResetGraph` y `CD_init`, y no volvió a emitir `VSync: timeout`. Tampoco registró errores, excepciones ni destinos desconocidos.

La desaparición del timeout confirma que la sustitución está activa. Por sí sola todavía no demuestra que el juego complete el arranque; la siguiente barrera deberá localizarse mediante observación y rastreo acotado.
