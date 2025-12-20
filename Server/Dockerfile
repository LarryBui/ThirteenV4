# 1) Build stage: build the Go runtime module with Nakama's plugin toolchain.
FROM heroiclabs/nakama-pluginbuilder:3.35.1 AS builder

WORKDIR /workspace

COPY go.mod go.sum ./
RUN go mod download

COPY . .
RUN go build --trimpath --buildmode=plugin -o /out/tienlen.so ./cmd/nakama

# 2) Runtime stage: run Nakama with the compiled module.
FROM heroiclabs/nakama:3.35.1

COPY --from=builder /out/tienlen.so /nakama/data/modules/tienlen.so
COPY local.yml /nakama/data/local.yml
COPY data/bot_identities.json /nakama/data/bot_identities.json
