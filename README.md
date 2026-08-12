# Receptor AirPlay para Windows 11

Permite espejar la pantalla de un iPhone, iPad o Mac contra una PC con Windows 11.
Pensado para aulas: se despliega una sola imagen en todos los equipos y cada uno
se anuncia con el nombre de su aula, derivado del hostname.

> **Estado:** el motor, la app de bandeja y el empaquetado portable están
> implementados y se compilan en CI. **Todavía no se validó en hardware real** —
> ver [Verificación pendiente](#verificación-pendiente).

## Cómo funciona

Son dos piezas separadas:

| Pieza | Qué es |
|---|---|
| **Motor** (`engine/`) | [UxPlay](https://github.com/FDH2/UxPlay) compilado para Windows con MSYS2, empaquetado con todas sus DLLs y plugins de GStreamer. Implementa el protocolo AirPlay: RTSP, handshake, descifrado y decodificación H.264/HEVC. |
| **App** (`src/AirPlayReceiver.App/`) | .NET 8 + WinForms. Ícono de bandeja que supervisa el proceso del motor, arma sus argumentos y resuelve el nombre del receptor. |

La app **no** implementa el protocolo AirPlay; lo hace todo el motor. La app
aporta lo que hace falta para desplegar esto en una institución: nombre por PC,
supervisión con reinicio automático, auto-inicio, regla de firewall y registro.

## Estructura

```
engine/          Scripts de build y empaquetado del motor (MSYS2/bash)
src/             App de bandeja .NET
tests/           Tests unitarios (xunit)
docs/            Guía de despliegue y configuración de ejemplo
.github/         CI: compila motor + app y publica el ZIP portable
```

## Compilar

### En CI (recomendado)

El workflow `build` compila todo en `windows-latest` y publica el artefacto
`ReceptorAirPlay-portable`. Es la forma de referencia: no requiere preparar nada
a mano.

### Localmente

**El motor** necesita [MSYS2](https://www.msys2.org). Desde una terminal
**UCRT64**:

```bash
./engine/build-engine.sh   # instala dependencias, clona y compila UxPlay
./engine/bundle.sh         # arma out/engine autocontenido
```

**La app** necesita el SDK de .NET 8:

```powershell
dotnet test tests/AirPlayReceiver.Tests/AirPlayReceiver.Tests.csproj
dotnet publish src/AirPlayReceiver.App/AirPlayReceiver.App.csproj -c Release -o out/app
```

Para ejecutar sin empaquetar, se puede apuntar la app a otra carpeta de motor:

```powershell
$env:AIRPLAY_ENGINE_DIR = "C:\ruta\a\out\engine"
```

### Versión de UxPlay

Se fija en `engine/uxplay.env`. Está en `master` a propósito y no en el último
tag publicado: la opción `USE_DNS_SD=OFF`, que hace que UxPlay compile su propio
mDNSResponder y elimina la dependencia del SDK de Bonjour de Apple, llegó
después de la v1.73.6. Sin eso el build no sería automatizable en CI.

El commit exacto compilado queda registrado en `engine-manifest.txt` dentro del
artefacto.

## Despliegue

Ver **[docs/DESPLIEGUE.md](docs/DESPLIEGUE.md)** — incluye los requisitos de red
(mDNS), la configuración del nombre por aula y el diagnóstico.

## Verificación pendiente

CI valida que todo compile, que los tests pasen y que `uxplay.exe -h` cargue con
las DLLs empaquetadas. Lo que **no** puede validar es el protocolo en sí. Falta
probar en hardware real:

1. Instalar el ZIP portable en una PC con Windows 11.
2. Confirmar que la PC aparece con el nombre esperado en el selector AirPlay de
   un iPad **en la misma subred**.
3. Confirmar que el espejado se ve y se escucha.

## Alcance

**Incluido:** espejado de pantalla (con su audio), un dispositivo por vez.

**No incluido:** modo de video nativo (YouTube/Netflix por URL), AirPlay de audio
independiente, varios dispositivos simultáneos.

## Licencia

GPLv3 — ver [LICENSE](LICENSE). El proyecto redistribuye UxPlay, que es GPLv3,
así que la distribución completa queda bajo esa licencia.

El handshake FairPlay que usa UxPlay se apoya en claves de Apple obtenidas por
ingeniería inversa. Es aceptable para uso interno; **no** lo es para un producto
comercial cerrado.
