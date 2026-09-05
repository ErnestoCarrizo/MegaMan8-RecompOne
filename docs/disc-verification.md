# Disco objetivo

La configuración inicial utiliza la edición estadounidense de Mega Man 8 para PlayStation.

- Identificador del ejecutable: `SLUS_004.53`
- Formato: una pista de datos `MODE2/2352` y dos pistas de audio
- Archivo de entrada: `Mega Man 8 (USA).cue`

## Huellas SHA-1

| Archivo | SHA-1 |
| --- | --- |
| `Mega Man 8 (USA).cue` | `2FBC5D7FA78EA63FDADFBB470678A37AC7232232` |
| `Mega Man 8 (USA) (Track 1).bin` | `21616C788D6BFC4A16C944A4CDA1A1862B8BCDA8` |
| `Mega Man 8 (USA) (Track 2).bin` | `70E4BDDD2A67AB7E2CCEA0C836C7E9190F307241` |
| `Mega Man 8 (USA) (Track 3).bin` | `D9F92AF296360772E62CAA4CB276DE3FA74F5538` |

## Ejecutable principal

El archivo `SLUS_004.53` fue leído directamente desde la pista de datos, sin conservar una copia extraída en el repositorio:

| Campo | Valor |
| --- | --- |
| LBA | `23` |
| Tamaño | `1.128.448` bytes |
| SHA-1 | `51BD31FD7B71328E085C91C374667CB07F0640C9` |
| Último byte no nulo del payload | `0x9296F` |
| Primer límite de página posterior | `0x93000` |
| Cola nula del archivo | `525.968` bytes |

El encabezado declara `0x113000` bytes de texto, pero una parte considerable funciona como memoria inicialmente puesta a cero y no contiene código estático.

Los archivos del disco no forman parte del repositorio. En esta máquina, `disc/` es una unión local hacia `D:\Megaman 8` y está excluida mediante `.gitignore`.
