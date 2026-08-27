#!/bin/sh
# Сборка Go-плагина под Nakama 3.22.0.
# Важно: у nakama-pluginbuilder ENTRYPOINT=go, поэтому --entrypoint /bin/sh.
set -eu
mkdir -p modules

docker run --rm \
  --entrypoint /bin/sh \
  -v "$PWD":/backend \
  -w /backend \
  heroiclabs/nakama-pluginbuilder:3.22.0 \
  -c 'go mod tidy && go build --trimpath --buildmode=plugin -o ./modules/backend.so .'

ls -la modules/backend.so
echo "OK: modules/backend.so готов. Дальше: docker compose restart nakama"
