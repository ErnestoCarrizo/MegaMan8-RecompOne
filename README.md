# Mega Man 8 — proyecto de aprendizaje con RecompOne

Proyecto educativo para estudiar el proceso de recompilación estática de la versión de PlayStation de Mega Man 8 mediante [RecompOne](https://github.com/BlackLabelHQ/RecompOne).

## Estado

El repositorio contiene solamente la estructura, configuración y código propio del port. No incluye archivos del juego ni código generado a partir de ellos.

La configuración inicial apunta a la edición estadounidense `SLUS-00453`. Las huellas de la copia utilizada están documentadas en `docs/disc-verification.md`.

El mapa inicial contiene 1876 funciones candidatas del ejecutable principal. La salida generada compila y alcanza la inicialización de GPU, CD y sincronización vertical. Este mapa proviene de un barrido lineal y todavía debe validarse; los overlays del juego aún no están declarados.

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
dotnet run --project .\RecompOne\RecompOne.Recompiler -c Release --no-build -- .\config\megaman8.json
dotnet build .\MegaMan8Recomp.csproj -c Release
```

Para iniciar la compilación de prueba:

```powershell
dotnet run --project .\MegaMan8Recomp.csproj -c Release --no-build -- ".\disc\Mega Man 8 (USA).cue"
```

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
