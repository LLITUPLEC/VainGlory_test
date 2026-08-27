# Сборка Go-плагина и образ Nakama должны быть одной версии.
# Основной путь на VPS — build-plugin.sh + docker-compose (как старый Lua-проект).
# Этот Dockerfile — запасной «всё в одном образе».
ARG NAKAMA_VERSION=3.22.0

FROM heroiclabs/nakama-pluginbuilder:${NAKAMA_VERSION} AS builder

ENV GO111MODULE=on
ENV CGO_ENABLED=1

WORKDIR /backend
COPY go.mod ./
COPY main.go ./
COPY match ./match
RUN go mod tidy && go build --trimpath --buildmode=plugin -o ./backend.so

FROM heroiclabs/nakama:${NAKAMA_VERSION}

COPY --from=builder /backend/backend.so /nakama/data/modules/
COPY local.yml /nakama/data/local.yml
