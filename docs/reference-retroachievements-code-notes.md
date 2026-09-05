# Notas de memoria de RetroAchievements

Fuentes:

- Juego: <https://retroachievements.org/game/11365>
- Notas: <https://retroachievements.org/codenotes.php?g=11365>
- Endpoint público consultado: `dorequest.php?r=codenotes2&g=11365`

La consulta realizada el 5 de septiembre de 2026 devolvió 122 notas atribuidas a `Pebete` y `Rafatenshii`.

## Conversión de direcciones

RetroAchievements presenta offsets físicos dentro de los 2 MiB de RAM de PlayStation. El código MIPS normalmente accede a su espejo KSEG0 sumando `0x80000000`:

```text
RA 0x15E283 -> MIPS 0x8015E283
RA 0x1C336E -> MIPS 0x801C336E
```

Estas direcciones describen datos en RAM, no comienzos de funciones. Son útiles para buscar qué funciones los leen o escriben y deducir responsabilidades.

## Estructura de entidades y enemigos

Las notas estadounidenses permiten inferir una estructura aproximada a partir de `0x8015B170`, repetida cada 95 bytes (`0x5F`):

| Offset | Dirección del primer elemento | Interpretación |
| ---: | ---: | --- |
| `+0x12` | `0x8015B182` | posición X, 16 bits |
| `+0x16` | `0x8015B186` | posición Y, 16 bits |
| `+0x2C` | `0x8015B19C` | puntero a tabla de sprites, 32 bits |
| `+0x48` | `0x8015B1B8` | ID de enemigo o jefe |
| `+0x4B` | `0x8015B1BB` | vida del enemigo |

Entre los IDs documentados figuran los ocho Robot Masters, Duo, Green Devil, Bass & Treble y la cápsula de Wily. Esto será útil para localizar actualización, colisiones, daño y renderizado de entidades. La nota japonesa que menciona una estructura de 92 bytes debe mantenerse separada porque puede corresponder a otro layout regional.

## Estructura del jugador

La agrupación que comienza aproximadamente en `0x8015E23D` contiene:

| Offset | Dirección | Interpretación |
| ---: | ---: | --- |
| `+0x00` | `0x8015E23D` | estado general: listo, jugando, muerto o terminando nivel |
| `+0x0D` | `0x8015E24A` | posición X, 16 bits |
| `+0x11` | `0x8015E24E` | posición Y, 16 bits |
| `+0x24` | `0x8015E261` | orientación izquierda/derecha |
| `+0x27` | `0x8015E264` | animación, 16 bits |
| `+0x42` | `0x8015E27F` | estado sobre el suelo |
| `+0x45` | `0x8015E282` | invulnerabilidad activa |
| `+0x46` | `0x8015E283` | vida del jugador |
| `+0x69` | `0x8015E2A6` | carga del Mega Buster |
| `+0x6E` | `0x8015E2AB` | temporizador de invulnerabilidad |
| `+0x76` | `0x8015E2B3` | sobre tabla |
| `+0x78` | `0x8015E2B5` | usando Rush Jet |
| `+0x92` | `0x8015E2CF` | bloqueo del control del jugador |

Nuestro código generado ya muestra numerosas referencias a posición, vida y estados de esta zona. Eso proporciona puntos de partida para investigar funciones, pero no justifica todavía asignarles nombres definitivos: una misma función puede leer varias variables globales y parte del mapa procede de un barrido lineal.

## Progreso, armas y estado persistente

Otros bloques de alto valor:

- `0x8016D2F0`: contador de tornillos; flags individuales en `0x8016D2FB..0x8016D2FF` y mejoras de Rush en `0x8016D300..0x8016D303`.
- `0x8016DC08`: arma seleccionada, con valores para Mega Buster y las nueve armas especiales.
- `0x801B1EB0`: bloque de disponibilidad y energía de armas; las entradas siguen un patrón regular de cuatro bytes.
- `0x801B2948`: contadores de proyectiles activos, seguido por visibilidad del HUD y varios estados de la fase.
- `0x801C3341..0x801C3355`: mejoras compradas o usadas.
- `0x801C336E`: ID de nivel.
- `0x801C3370`: vidas.
- `0x801C3374`: mitad de la fase antes o después del checkpoint.
- `0x801C3377..0x801C3378`: progreso de los acertijos de Sword Man.

Como ejemplo de referencias estáticas ya visibles, `func_800FA6AC` consulta tanto la vida del jugador como las vidas restantes; `func_800FA864` consulta el arma actual; y `func_800FF948` consulta el ID de nivel. Son candidatos para rastreo dirigido, no nombres confirmados.

## Relación con el límite estático

La mayoría de estos datos comienza después de `0x80153000`, que es precisamente el final verificado del código y datos no nulos del ejecutable. Esto refuerza que la cola declarada por el PS-X EXE es almacenamiento inicializado a cero utilizado durante el juego. Eliminar `func_8017D5D4` del mapa de funciones no elimina esa RAM: solo evita recompilarla erróneamente como instrucciones.

## Uso previsto

Estas notas servirán para:

1. buscar referencias de lectura y escritura en las funciones recompiladas;
2. reconocer funciones de inicialización, actualización, daño, inventario y cambio de fase;
3. validar el contenido de RAM durante pruebas del port;
4. dar nombres a estructuras y variables globales antes de nombrar funciones;
5. construir más adelante pruebas de estado comparables con un emulador de referencia.

Las notas marcadas explícitamente como japonesas no deben aplicarse a `SLUS-00453` sin una comprobación adicional.
