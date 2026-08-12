# Despliegue en las PCs del aula

## Requisitos de red (leer primero)

El descubrimiento AirPlay usa **mDNS** (multicast DNS, UDP 5353). Es el punto de
falla más común y **no se arregla desde la app**:

- El iPad y la PC tienen que estar en la **misma VLAN / subred**.
- La red **no** puede tener aislamiento de clientes (*client isolation* / *AP
  isolation*), típico de las redes de invitados.
- El tráfico multicast tiene que estar permitido entre los puertos del switch.

Si el equipo no aparece en el selector AirPlay del iPad y el registro del motor
no muestra errores, el problema casi siempre está acá.

## Instalación

1. Copiar el contenido del ZIP portable a `C:\Program Files\ReceptorAirPlay\`.
2. Ejecutar `AirPlayReceiver.exe` una vez y usar **Reparar regla de firewall**
   (pide elevación). Sin esa regla el equipo no responde al descubrimiento.
3. Activar **Iniciar con Windows** desde el menú de la bandeja.

## Nombre del aula sin configurar equipo por equipo

Es la razón por la que existe la plantilla de nombres: se despliega **un solo**
archivo de configuración a todas las PCs y cada una deriva su nombre del
hostname que ya tiene asignado.

Copiar `config.ejemplo.json` a:

```
C:\ProgramData\GoetheAirPlay\config.json
```

Con la configuración de ejemplo, un equipo llamado `PC-AULA-203` se publica como
**«Aula 203»**.

Ajustar `hostnamePattern` a la convención real de nombres de la institución. Los
grupos con nombre de la regex quedan disponibles como tokens en `nameTemplate`;
también sirven los grupos numerados (`{1}`, `{2}`) y cualquier variable de
entorno.

Si el hostname no matchea el patrón (por ejemplo una notebook de dirección), se
usa `fallbackNameTemplate` en vez de publicar un nombre a medio armar.

### Precedencia de configuración

| Ubicación | Uso |
|---|---|
| `%ProgramData%\GoetheAirPlay\config.json` | Config de máquina. **Gana entera.** |
| `%LOCALAPPDATA%\GoetheAirPlay\config.json` | Config por usuario. Solo se usa si no hay config de máquina. |

La de máquina no se fusiona campo por campo con la de usuario: o manda una o
manda la otra. Es deliberado, para que un despliegue por GPO garantice el
nombre del aula sin estados intermedios difíciles de diagnosticar.

La configuración se relee **cada vez que arranca el motor**, así que un cambio
desplegado se aplica sin reinstalar.

## Diagnóstico

El registro está en:

```
%LOCALAPPDATA%\GoetheAirPlay\logs\receptor.log
```

Incluye la línea de comandos con la que se lanzó el motor y toda su salida.
También se llega desde el menú de la bandeja, en **Ver registro**.

`engine\engine-manifest.txt` indica exactamente qué commit de UxPlay y qué
versiones de GStreamer se empaquetaron — útil cuando una actualización de iOS
rompe algo y hay que comparar contra un build anterior.

## Limitaciones conocidas

- **Solo espejado de pantalla.** No está implementado el modo de video nativo
  (el de YouTube/Netflix, donde el dispositivo envía la URL en vez de espejar).
- **Un dispositivo por vez.** No hay soporte multi-dispositivo simultáneo.
- **El protocolo puede romperse con actualizaciones de iOS.** Apple no lo
  documenta; todo viene de ingeniería inversa. Ante una falla tras actualizar
  iOS, lo primero es probar un build con un UxPlay más nuevo.
