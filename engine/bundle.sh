#!/usr/bin/env bash
#
# Arma una carpeta autocontenida con uxplay.exe, sus DLLs y los plugins de
# GStreamer, de forma que el motor corra en una PC sin MSYS2 ni GStreamer
# instalados.
#
# Uso, desde una terminal UCRT64 de MSYS2, despues de build-engine.sh:
#     ./engine/bundle.sh [directorio-de-salida]
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WORK_DIR="${SCRIPT_DIR}/work"
OUT_DIR="${1:-${REPO_ROOT}/out/engine}"

if [[ ! -f "${WORK_DIR}/build-info.env" ]]; then
  echo "ERROR: falta ${WORK_DIR}/build-info.env. Ejecuta primero build-engine.sh." >&2
  exit 1
fi
# shellcheck source=/dev/null
source "${WORK_DIR}/build-info.env"

PREFIX="${MINGW_PREFIX:?MINGW_PREFIX no definido; ejecuta desde una terminal MinGW de MSYS2}"
PLUGIN_SRC="${PREFIX}/lib/gstreamer-1.0"

echo "==> Empaquetando en ${OUT_DIR}"
rm -rf "${OUT_DIR}"
mkdir -p "${OUT_DIR}/lib/gstreamer-1.0"

# --- Ejecutable ------------------------------------------------------------
cp -f "${UXPLAY_EXE}" "${OUT_DIR}/uxplay.exe"

# --- Plugins de GStreamer --------------------------------------------------
# Se copian todos los plugins instalados en vez de una lista curada: UxPlay
# arma su pipeline en runtime y elige elementos segun el codec que negocie el
# dispositivo (H.264 o HEVC), asi que una lista corta se rompe en silencio con
# ciertos modelos de iPhone/iPad.
if [[ ! -d "${PLUGIN_SRC}" ]]; then
  echo "ERROR: no existe ${PLUGIN_SRC}. Faltan los paquetes de GStreamer." >&2
  exit 1
fi
plugin_count=$(find "${PLUGIN_SRC}" -maxdepth 1 -name '*.dll' | wc -l)
echo "==> Copiando ${plugin_count} plugins de GStreamer"
find "${PLUGIN_SRC}" -maxdepth 1 -name '*.dll' -exec cp -f {} "${OUT_DIR}/lib/gstreamer-1.0/" \;

# gst-plugin-scanner es un ejecutable auxiliar que GStreamer lanza para
# inspeccionar los plugins; sin el, el registro queda vacio y no hay video.
SCANNER="$(find "${PREFIX}/lib/gstreamer-1.0" "${PREFIX}/libexec/gstreamer-1.0" \
  -maxdepth 1 -name 'gst-plugin-scanner.exe' -print -quit 2>/dev/null || true)"
if [[ -n "${SCANNER}" ]]; then
  cp -f "${SCANNER}" "${OUT_DIR}/lib/gstreamer-1.0/"
  echo "==> gst-plugin-scanner.exe incluido"
else
  echo "ADVERTENCIA: no se encontro gst-plugin-scanner.exe" >&2
fi

# --- Cierre transitivo de DLLs --------------------------------------------
# ldd resuelve solo las dependencias directas de cada binario. Los plugins
# tienen las suyas propias (libav, codecs, etc.), asi que se itera hasta que
# una pasada completa no agregue ninguna DLL nueva.
echo "==> Resolviendo dependencias DLL"
pass=0
while :; do
  pass=$((pass + 1))
  added=0

  while IFS= read -r binary; do
    # ldd falla en binarios que no puede leer; no debe cortar el script.
    while IFS= read -r dep; do
      [[ -z "${dep}" ]] && continue
      name="$(basename "${dep}")"
      if [[ ! -f "${OUT_DIR}/${name}" ]]; then
        cp -f "${dep}" "${OUT_DIR}/${name}"
        added=$((added + 1))
      fi
    done < <(ldd "${binary}" 2>/dev/null \
             | awk '{print $3}' \
             | grep -i "^${PREFIX}/bin/" || true)
  done < <(find "${OUT_DIR}" -name '*.exe' -o -name '*.dll')

  echo "    pasada ${pass}: ${added} DLL nuevas"
  [[ "${added}" -eq 0 ]] && break
  if [[ "${pass}" -ge 20 ]]; then
    echo "ERROR: el cierre de dependencias no converge." >&2
    exit 1
  fi
done

dll_count=$(find "${OUT_DIR}" -maxdepth 1 -name '*.dll' | wc -l)
echo "==> ${dll_count} DLLs empaquetadas"

# --- Manifiesto de trazabilidad -------------------------------------------
{
  echo "UxPlay ref:       ${UXPLAY_REF}"
  echo "UxPlay describe:  ${UXPLAY_DESC}"
  echo "UxPlay commit:    ${UXPLAY_SHA}"
  echo "MSYSTEM:          ${MSYSTEM}"
  echo "Compilado:        $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo ""
  echo "Versiones de paquetes MSYS2:"
  pacman -Q 2>/dev/null | grep -E 'gstreamer|gst-plugins|gst-libav|libplist|openssl' || true
} > "${OUT_DIR}/engine-manifest.txt"

echo "==> Listo. Motor autocontenido en ${OUT_DIR}"
