# Referencia externa: `douglasjv/mm8`

Repositorio analizado: <https://github.com/douglasjv/mm8>, commit `4eae0f952df9a527d18ff1916d373619fbb1a391`.

## Compatibilidad de versión

La referencia usa la edición estadounidense `SLUS-00453`. Su ejecutable principal tiene SHA-1 `51BD31FD7B71328E085C91C374667CB07F0640C9`, exactamente igual al extraído en memoria desde nuestra pista de datos. También coinciden:

- LBA del ejecutable: `23`;
- tamaño del archivo: 1.128.448 bytes;
- dirección de carga: `0x800C0000`;
- entrada: `0x800C0B3C`;
- pila inicial: `0x801FFFF0`;
- último byte no nulo del payload: `0x9296F`.

Esto permite reutilizar direcciones obtenidas sobre el ejecutable estático. No garantiza que los offsets físicos de las pistas de disco sean idénticos: el SHA-1 publicado para su pista de datos no coincide con el del BIN MODE2/2352 local.

## Símbolos disponibles

El repositorio no publica un mapa de funciones ni símbolos descubiertos. `seeds/main.txt` contiene únicamente `0x800C0B3C`, la entrada del ejecutable. Por eso no es posible importar nombres de funciones en bloque.

Sí aporta un candidato semántico útil: los tres sitios del renderer de fondo de la fase inicial (`0x800F993C`, `0x800F994C` y `0x800F99CC`) caen dentro de nuestra `func_800F98D8`, de 288 bytes. La referencia observó que esa rutina trabaja con 21 columnas de tiles de 16 píxeles. El nombre provisional sugerido sería `RenderOpeningStageBackgroundTiles`, pero no debe aplicarse todavía: el propio informe indica que el código puede ejecutarse desde RAM modificada y debe validarse con nuestro rastreo.

La dirección `0x801D28F8` es un candidato a dato global de estado de escena, no a función: se observó el valor `0x10` en título/menú y `0x01` durante la fase inicial jugable.

## Hallazgos relevantes para RecompOne

### Límite real del análisis estático

Aunque el encabezado PS-X EXE declara `0x113000`, el último byte no nulo del payload está en `0x9296F`. La referencia usa el límite alineado `0x93000` para no interpretar la extensa cola nula como código.

Nuestro mapa confirma el problema: contiene una entrada espuria `func_8017D5D4` de 350.764 bytes, situada después del límite real. Conviene eliminarla o regenerar el mapa con el límite correcto en un paso separado y comparar el resultado antes de adoptarlo.

### Overlays y código escrito en RAM

La referencia registró código transmitido desde el disco y regiones reescritas durante la ejecución, incluidas páginas alrededor de `0x800D8000`. Su runtime combina recompilación, intérprete y captura dinámica de overlays. Nuestro archivo `megaman8.json` todavía declara `overlays: []`; identificar las cargas desde CD y modelar esos overlays será una tarea central para superar el arranque y cubrir fases completas.

Los sitios `0x800F993C`, `0x800F994C` y `0x800F99CC` deben tratarse además como dependientes de la versión cargada en RAM, no solamente de los bytes iniciales del ejecutable.

### Comportamiento ya investigado

- La simulación jugable fue observada a 59,94 Hz; no corresponde aplicar un parche de “60 FPS” que duplicaría la velocidad del juego.
- El juego usa control digital y entradas activas en nivel bajo. Exponer ejes analógicos no crearía movimiento proporcional por sí solo.
- Para widescreen se identificó un rango de paquetes del HUD en RAM física `0x00152974..0x00152DD8`, pero es una mejora futura y no una necesidad del bring-up.

## Limitaciones de la referencia

El proyecto tenía solamente dos commits al momento del análisis. Su submódulo `psxrecomp` apunta a `douglasjv/psxrecomp-tweaks`, que no estaba accesible públicamente, y no incluye el código generado, capturas ni informes de ejecución. Los resultados publicados son pistas valiosas y algunas direcciones son verificables sobre nuestro binario, pero la implementación completa no puede reproducirse únicamente con los archivos públicos actuales.
