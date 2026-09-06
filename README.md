# Mega Man 8 — proyecto de aprendizaje con RecompOne

Proyecto educativo para estudiar el proceso de recompilación estática de la versión de PlayStation de Mega Man 8 mediante [RecompOne](https://github.com/BlackLabelHQ/RecompOne).

## Estado

El repositorio contiene solamente la estructura, configuración y código propio del port. No incluye archivos del juego ni código generado a partir de ellos.

La configuración inicial apunta a la edición estadounidense `SLUS-00453`. Las huellas de la copia utilizada están documentadas en `docs/disc-verification.md`.

Las direcciones de juego conocidas obtenidas durante el desarrollo del set de RetroAchievements están resumidas en `docs/reference-retroachievements-code-notes.md` y se usarán como guía para identificar datos y funciones.

El mapa principal contiene 1875 funciones candidatas dentro del límite estático verificado `0x93000`. Se identificaron `VSync`, siete entradas de `libcd`, las ocho entradas de streaming STR, la biblioteca gráfica principal, rutinas de control, el planificador de cuatro tareas y el dispatcher inicial de escenas. RecompOne aplica 21 sustituciones HLE automáticas y adaptaciones locales para el CD, las tareas cooperativas y la transición gráfica. El port alcanza el título, ejecuta demostraciones completas y también inicia la ruta de una partida nueva después de su cinemática. Las películas STR del arranque y de la partida fueron comprobadas visualmente mediante capturas de VRAM. Se declararon el overlay del título y tres overlays usados por las demos; el primero también se reutiliza en la ruta de partida. El estado más reciente se detalla en `docs/bringup-step-11-video-validation-and-new-game.md`.

## Requisitos

- .NET SDK 10
- Git con soporte para submódulos
- Una copia propia del juego en formato CUE/BIN

## Preparación

Después de clonar este repositorio:

```powershell
git submodule update --init --recursive
dotnet build .\RecompOne\RecompOne.sln -c Release
```

## Generar y compilar el port

Con una copia compatible del disco disponible en `disc/`:

```powershell
.\scripts\recompile.ps1
.\scripts\build-port.ps1 -SkipRecompile
```

Para regenerar, compilar e iniciar el port en una sola operación:

```powershell
.\scripts\run-port.ps1
```

`run-port.ps1` guarda automáticamente la salida del juego en `logs/`. Para ejecutar un resultado ya compilado sin regenerarlo, se puede utilizar `run-port.ps1 -SkipBuild`. El modificador `-Quiet` envía la salida solamente al archivo, algo especialmente útil con el rastreo detallado de funciones.

Para observar el avance por cuadro, los llamadores de `VSync`, variables conocidas y posibles modificaciones de código:

```powershell
.\scripts\run-port.ps1 -BringupTrace
```

`-SdkLog` añade el registro detallado de llamadas SDK y del controlador de CD. Ambos diagnósticos están desactivados durante una ejecución normal.

`-VideoLog` registra MDEC y sus transferencias DMA. `-VideoSnapshots` guarda cuadros de las películas en `logs/video-snapshots/`; estas capturas de la sombra de VRAM no incluyen los sprites rasterizados directamente por el backend acelerado. `-AutoProgress` ejecuta una secuencia de entrada de diagnóstico para atravesar el título e iniciar una partida nueva.

Los scripts resuelven sus rutas desde la raíz del repositorio, por lo que también pueden invocarse desde otra carpeta. Todos aceptan `-Configuration Debug` o `-Configuration Release`.

## Estructura prevista

- `config/`: configuración del recompilador.
- `config/funcmaps/`: direcciones y nombres de funciones identificadas.
- `patches/`: correcciones y adaptaciones escritas en C#.
- `RecompOne/`: versión fijada del recompilador y runtime como submódulo.
- `disc/`: imagen local del juego; está excluida de Git.
- `generated/`: código producido por el recompilador; está excluido de Git.

En el equipo de desarrollo, `disc/` puede ser una unión hacia una carpeta externa para evitar duplicar la imagen del juego.

## Material protegido

Los archivos CUE, BIN, ISO, CHD y el código generado desde ellos no deben añadirse al repositorio. Cada usuario deberá aportar su propia copia legal del juego para construir el port.
