# Paso 4: límite del análisis estático

## Problema

El encabezado de `SLUS_004.53` declara un payload de `0x113000` bytes, pero el último byte no nulo está en `0x9296F`. El resto es memoria inicialmente puesta a cero y no debe interpretarse como instrucciones MIPS.

El barrido original del rango completo había creado `func_8017D5D4`, una función ficticia de 350.764 bytes que se extendía hasta el final del rango declarado.

## Comprobación antes de modificar el mapa

Se generó un mapa candidato mediante el mismo barrido lineal, cambiando únicamente el tamaño analizado de `0x113000` a `0x93000`:

```powershell
dotnet run --project .\RecompOne\RecompOne.Recompiler\RecompOne.Recompiler.csproj `
  -c Release --no-build -- `
  --generate-function-file -linear-sweep `
  -disc ".\disc\Mega Man 8 (USA).cue" `
  -file "SLUS_004.53" -base 800C0000 -skip 800 -size 93000 `
  -out <mapa-candidato.json>
```

La comparación estructural produjo:

```text
Mapa anterior:       1876 funciones
Mapa candidato:      1875 funciones
Entradas eliminadas: 1
Entradas añadidas:   0
Tamaños modificados: 0
```

La única entrada eliminada fue `func_8017D5D4`. Como el mapa candidato no conserva nombres verificados, se aplicó esa diferencia mínima sobre el mapa existente en lugar de reemplazarlo; de ese modo se mantuvo `VSync`.

## Resultado

El mapa termina ahora en `func_80151724`, dentro del rango estático real, y contiene 1.875 funciones. Este ajuste solo elimina código nulo mal clasificado; no intenta resolver todavía las tablas de datos intercaladas ni el código cargado dinámicamente desde el disco.

La regeneración posterior informó 1.876 funciones totales —las 1.875 del mapa más la entrada técnica del ejecutable—, 13 funciones con tablas de salto, 276 destinos de tabla y una reimplementación HLE. La compilación Release terminó correctamente.

Una ejecución de doce segundos volvió a inicializar OpenGL, cargar `main`, ejecutar `ResetGraph` y `CD_init`. El registro no contiene timeouts, errores, excepciones ni destinos desconocidos, por lo que la eliminación no produjo una regresión observable en esta etapa del arranque.
