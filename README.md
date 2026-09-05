# Mega Man 8 — proyecto de aprendizaje con RecompOne

Proyecto educativo para estudiar el proceso de recompilación estática de la versión de PlayStation de Mega Man 8 mediante [RecompOne](https://github.com/BlackLabelHQ/RecompOne).

## Estado

El repositorio contiene solamente la estructura, configuración y código propio del port. No incluye archivos del juego ni código generado a partir de ellos.

La configuración inicial apunta a la edición estadounidense `SLUS-00453`. Las huellas de la copia utilizada están documentadas en `docs/disc-verification.md`.

Las direcciones de juego conocidas obtenidas durante el desarrollo del set de RetroAchievements están resumidas en `docs/reference-retroachievements-code-notes.md` y se usarán como guía para identificar datos y funciones.

El mapa contiene 1875 funciones candidatas dentro del límite estático verificado `0x93000`. `VSync` ya fue identificada y sustituida por la implementación HLE de RecompOne; la salida generada compila y supera el timeout inicial de sincronización vertical. El siguiente bloqueo está localizado en la lectura asíncrona del primer recurso del CD: llega el primer sector, pero no los siguientes. El diagnóstico está detallado en `docs/bringup-step-5-runtime-monitor.md`. Este mapa proviene de un barrido lineal y todavía debe validarse; los overlays del juego aún no están declarados.

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
