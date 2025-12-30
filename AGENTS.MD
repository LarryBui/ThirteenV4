* always run unit test
* For Server changes: always rebuild nakama and refresh docker (docker-compose up --build -d nakama)
* do not change code until explicitly approved ##proposal => approval => execute
* once your proposal is approved, you have full permission to make changes on what has been approved; you don't need to ask for permission on cli command
* always use Clean Architecture, best practices, documentation when generating code or doing reasoning thinking
* For this specific project, always separate domain, application, presenter, views, Nakama(server) interface layer
* avoid making god classes


# AGENTS.md

## 1. Project Context: Tien Len (Thirteen)
- **Frontend:** Unity (C#)
- **Backend:** Nakama Server (Go-based distributed modules)
- **Database:** PostgreSQL (via Nakama's storage engine)
- **Architecture:** Hybrid Clean Architecture/DDD.
  - **Source of Truth:** Nakama Go Modules (Server-Authoritative).
  - **Client:** Unity acts as a View/Controller with local state prediction.

## 2. Architectural Guardrails (Clean Architecture & DDD)
### Domain Isolation
- **Go (Backend):** The core rules of Tien Len (card ranking, sequence validation, winning conditions) must reside in a pure Go package (`/internal/domain`) with **no** Nakama-specific dependencies.
- **Unity (Frontend):** Game logic must be decoupled from `MonoBehaviours`. Use pure C# classes for card logic to allow for unit testing outside the Unity lifecycle.

### Data Integrity
- **Strict Validation:** Every move sent from Unity must be validated against the **Domain State** in Nakama. Never trust client-side calculations for game-ending logic.
- **Repository Pattern:** Use an abstraction over Nakama's `StorageWrite` and `StorageRead` to manage persistence (user stats, match history).

## 3. Nakama + Go Implementation Rules
- **Match Loop:** Implement the `MatchLoop` in Go to handle tick-based state broadcasts.
- **Concurrency:** Use Go channels and mutexes safely. Avoid global variables; maintain state within the Nakama `MatchState` struct.
- **Protocols:** Use JSON or Protobuf for messages. Ensure that any change to the Go message struct is reflected in the Unity C# DTOs immediately.

## 4. Unity (C#) Coding Standards
- **Data-Driven UI:** The UI must react to the `GameState` DTO received from Nakama. Do not couple UI logic directly to input logic.
- **Async/Await:** Use `UniTask` or standard `Task` for all network calls. Do not use blocking calls or coroutines for API requests.
- **Event-Driven:** Use a local `EventBus` to decouple network messages from UI updates (e.g., `OnCardPlayed`, `OnTurnChanged`).

## 5. Specific Game Rules: Tien Len
- **Card Ranking:** 2 (Highest) > A > K > Q > J > 10 > 9 > 8 > 7 > 6 > 5 > 4 > 3 (Lowest).
- **Suit Ranking:** Hearts > Diamonds > Clubs > Spades.
- **Validation:**
  - Valid combinations: Singles, Pairs, Triples, Sequences (3+), Bombs (Four of a Kind or 3+ pairs in sequence).
  - "Chopping": Bombs can defeat 2s.
- **Turn Logic:** Strict turn-based state machine. Passing skips the player until the round resets.

## 6. Instructions for the Agent (Jules)
1. **Context First:** Before writing code, analyze if the change affects the Shared Protocol (Network Messages) or Core Domain logic.
2. **Go Modifications:** When modifying backend logic, ensure `MatchState` is updated transactionally and a broadcast is sent.
3. **Unity Modifications:** When creating a new UI component, ensure it subscribes to the Event Bus and does not hold its own state.
4. **Synchronization:** If game rules change (e.g., Bomb logic), update **both** the Go validator and the Unity client predictor.