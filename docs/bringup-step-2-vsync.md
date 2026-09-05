# Paso 2: identificación de `VSync`

## Objetivo

Localizar la implementación de PsyQ `VSync` que está incluida en el ejecutable original. RecompOne puede reemplazar esta función por una implementación nativa del runtime, pero para activar ese reemplazo el mapa de funciones debe contener el nombre exacto `VSync`.

## Procedimiento

Se activó temporalmente la opción `debug` del recompilador. Así, cada función generada escribió su dirección al entrar. El port se ejecutó durante aproximadamente diez segundos y se capturaron 23,90 MB de rastreo local. El primer mensaje `VSync: timeout` apareció en la línea 144.

Las últimas llamadas inmediatamente anteriores al mensaje fueron:

```text
func_800CECB4
func_800D6654
func_800CF4A0
func_800CDF08
func_800CE050
func_800CE854
func_800CE050
func_800D65B4
VSync: timeout
```

## Resultado

La función pública `VSync` comienza en `0x800CDF08` y ocupa 328 bytes. La identificación se apoya en su comportamiento, no solamente en la proximidad del mensaje:

- recibe el modo en el registro `A0`;
- para un modo negativo devuelve el contador vertical sin esperar;
- para el modo `1` devuelve el tiempo transcurrido;
- para `0` o valores positivos calcula el cuadro objetivo y espera;
- entrega el resultado en `V0`.

Ese contrato coincide con el que espera `RecompOne.Runtime.Sdk.LibEtc.VSync`.

La función `0x800CE050`, de 156 bytes, no es la entrada pública. Es un ayudante interno de espera utilizado dos veces por `0x800CDF08`. Cuando detecta el límite de espera, pasa la cadena `VSync: timeout` a la rutina de impresión ubicada en `0x800D65B4`.

## Estado al terminar

El rastreo detallado volvió a quedar desactivado. El mapa todavía conserva el nombre provisional `func_800CDF08`: el cambio a `VSync` y la comprobación de que RecompOne aplique su reemplazo HLE corresponden al paso siguiente.
