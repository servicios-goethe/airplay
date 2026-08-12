#!/usr/bin/env bash
#
# Compila UxPlay dentro de MSYS2 (MinGW) y deja el ejecutable en engine/work/install.
# El empaquetado autocontenido (DLLs + plugins) lo hace bundle.sh despues.
#
# Uso, desde una terminal UCRT64 de MSYS2:
#     ./engine/build-engine.sh [--skip-deps]
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WORK_DIR="${SCRIPT_DIR}/work"
SRC_DIR="${WORK_DIR}/UxPlay"
BUILD_DIR="${WORK_DIR}/build"
INSTALL_DIR="${WORK_DIR}/install"

SKIP_DEPS=0
[[ "${1:-}" == "--skip-deps" ]] && SKIP_DEPS=1

# shellcheck source=/dev/null
source "${SCRIPT_DIR}/uxplay.env"

# --- Prefijo de paquetes segun el entorno MSYS2 activo ---------------------
case "${MSYSTEM:-}" in
  UCRT64)       PKG_PREFIX="mingw-w64-ucrt-x86_64-" ;;
  MINGW64)      PKG_PREFIX="mingw-w64-x86_64-" ;;
  CLANGARM64)   PKG_PREFIX="mingw-w64-clang-aarch64-" ;;
  *)
    echo "ERROR: MSYSTEM='${MSYSTEM:-<vacio>}' no soportado." >&2
    echo "Abri una terminal UCRT64 (o CLANGARM64 para ARM) y volve a ejecutar." >&2
    exit 1
    ;;
esac
echo "==> Entorno MSYS2: ${MSYSTEM} (prefijo de paquetes: ${PKG_PREFIX})"

# --- Dependencias ----------------------------------------------------------
if [[ "${SKIP_DEPS}" -eq 0 ]]; then
  mapfile -t PKG_NAMES < <(grep -v -e '^\s*#' -e '^\s*$' "${SCRIPT_DIR}/packages.txt")
  PACKAGES=("${PKG_NAMES[@]/#/${PKG_PREFIX}}")
  echo "==> Instalando ${#PACKAGES[@]} paquetes con pacman"
  pacman -S --needed --noconfirm "${PACKAGES[@]}"
else
  echo "==> --skip-deps: se omite la instalacion de paquetes"
fi

# --- Codigo fuente de UxPlay ----------------------------------------------
mkdir -p "${WORK_DIR}"
if [[ -d "${SRC_DIR}/.git" ]]; then
  echo "==> Actualizando checkout existente de UxPlay"
  git -C "${SRC_DIR}" fetch --tags --force origin
else
  echo "==> Clonando UxPlay desde ${UXPLAY_REPO}"
  git clone "${UXPLAY_REPO}" "${SRC_DIR}"
fi

echo "==> Checkout de UxPlay en '${UXPLAY_REF}'"
git -C "${SRC_DIR}" checkout --force "${UXPLAY_REF}"
# Si el ref es una rama, traer los ultimos commits; si es un tag o SHA, no aplica.
git -C "${SRC_DIR}" pull --ff-only origin "${UXPLAY_REF}" 2>/dev/null || true

UXPLAY_SHA="$(git -C "${SRC_DIR}" rev-parse HEAD)"
UXPLAY_DESC="$(git -C "${SRC_DIR}" describe --tags --always 2>/dev/null || echo "${UXPLAY_SHA}")"
echo "==> UxPlay ${UXPLAY_DESC} (${UXPLAY_SHA})"

# --- Compilacion -----------------------------------------------------------
# USE_DNS_SD=OFF -> UxPlay compila su propio mDNSResponder (lib/mdnsd) y no
# hace falta el SDK de Bonjour de Apple. Es lo que hace posible el build en CI.
echo "==> Configurando con CMake"
rm -rf "${BUILD_DIR}" "${INSTALL_DIR}"
cmake -S "${SRC_DIR}" -B "${BUILD_DIR}" \
  -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_INSTALL_PREFIX="${INSTALL_DIR}" \
  -DUSE_DNS_SD=OFF

echo "==> Compilando"
cmake --build "${BUILD_DIR}" --parallel

echo "==> Instalando en ${INSTALL_DIR}"
cmake --install "${BUILD_DIR}"

# UxPlay puede instalar el binario en bin/ o dejarlo en la raiz del build.
UXPLAY_EXE="$(find "${INSTALL_DIR}" "${BUILD_DIR}" -maxdepth 2 -name 'uxplay.exe' -print -quit)"
if [[ -z "${UXPLAY_EXE}" ]]; then
  echo "ERROR: no se encontro uxplay.exe despues de compilar." >&2
  exit 1
fi
echo "==> uxplay.exe: ${UXPLAY_EXE}"

# Datos para que bundle.sh genere el manifiesto de trazabilidad.
cat > "${WORK_DIR}/build-info.env" <<EOF
UXPLAY_EXE=${UXPLAY_EXE}
UXPLAY_SHA=${UXPLAY_SHA}
UXPLAY_DESC=${UXPLAY_DESC}
UXPLAY_REF=${UXPLAY_REF}
MSYSTEM=${MSYSTEM}
EOF

echo "==> Motor compilado. Siguiente paso: ./engine/bundle.sh"
