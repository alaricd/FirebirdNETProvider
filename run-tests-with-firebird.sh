#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
container="firebird-netprovider-tests"
image="${FIREBIRD_TEST_IMAGE:-firebirdsql/firebird}"
project="${FIREBIRD_TEST_PROJECT:-src/NETProvider.slnx}"

cleanup() {
    docker rm -f "${container}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

if ! command -v docker >/dev/null 2>&1; then
    echo "error: Docker is required" >&2
    exit 1
fi

cleanup
docker run --detach --name "${container}" \
    --publish 3050:3050 \
    --env ISC_PASSWORD=masterkey \
    --env FIREBIRD_ROOT_PASSWORD=masterkey \
    --env FIREBIRD_CONF_WireCrypt=Enabled \
    "${image}" >/dev/null

for attempt in $(seq 1 60); do
    if timeout 1 bash -c '</dev/tcp/127.0.0.1/3050' >/dev/null 2>&1; then
        break
    fi

    if [[ "${attempt}" == 60 ]]; then
        echo "error: Firebird did not become ready" >&2
        docker logs "${container}" >&2 || true
        exit 1
    fi
    sleep 1
done

cd "${root}"
FIREBIRD_DOCKER_TESTS=1 dotnet test "${project}" --no-restore "$@"
