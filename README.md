# ThirteenV4

## Wire format (Protobuf)
- Protocol definitions live in `proto/tienlen.proto`; Go stubs generated to `Server/proto/tienlen.pb.go`.
- Nakama opcodes stay unchanged and map to protobuf messages:
  - `OpStartGame` -> `StartGameRequest` (empty)
  - `OpPlayCards` -> `PlayCardsRequest`
  - `OpPassTurn` -> `PassTurnRequest` (empty)
  - `OpRequestNewGame` -> `RequestNewGameRequest` (empty)
  - `OpPlayerJoined` -> `MatchStateSnapshot`
  - `OpPlayerLeft` -> `PlayerLeftEvent`
  - `OpGameStarted` -> `GameStartedEvent`
  - `OpHandDealt` -> `HandDealtEvent`
  - `OpCardPlayed` -> `CardPlayedEvent`
  - `OpTurnPassed` -> `TurnPassedEvent`
  - `OpGameEnded` -> `GameEndedEvent`
- Regenerate Go stubs (from repo root): `protoc --go_out=Server --go_opt=paths=source_relative proto/tienlen.proto`
- Generate C# for Unity (example): `protoc --csharp_out=Client/Assets/Scripts/Proto proto/tienlen.proto` (ensure Google.Protobuf runtime is present).

## Server architecture (under `Server/`)
- Entry point: `cmd/nakama/main.go` exports `InitModule` and delegates to internal packages.
- Ports/Adapters: `internal/ports/nakama` handles Nakama runtime wiring, protobuf marshal/unmarshal, and opcode dispatch.
- Application: `internal/app` contains use-cases (join/leave/start, play, pass, reset) and emits domain events.
- Domain: `internal/domain` holds pure game state and helpers (deck, seats, label, counters) with no Nakama dependencies.

## Authentication & onboarding
- Device authentication uses the client-provided device ID as-is; do not randomize it server-side.
- On first creation (`session.Created == true`), the server assigns a friendly display name and grants a welcome gold bonus.
- The welcome bonus is idempotent; a storage marker `onboarding/welcome_bonus_v1` prevents double-granting.
- Clients should persist a stable device ID (Keychain/Keystore) and set `Create=true` only for first-time sign-in.

## Build and test (from `Server/`)
- Tests: `go test ./...`
## Docker (from `Server/`)
- Build & run: `cd Server; docker compose up -d --build`
- Logs: `cd Server; docker compose logs -f --tail 100 nakama`

## Vivox credentials (local)
Create a repo-root `.env` file with:
```
VIVOX_SECRET=your-vivox-secret
VIVOX_ISSUER=your-vivox-issuer
VIVOX_DOMAIN=your-vivox-domain
```
`Server/docker-compose.yml` loads this file when starting Nakama.

## Vivox token RPC
The client requests Vivox JWTs from the server via `get_vivox_token`:
- Login token: `{"action":"login"}`
- Join token: `{"action":"join","match_id":"<match_id>"}`
Response: `{"token":"<jwt>"}`
