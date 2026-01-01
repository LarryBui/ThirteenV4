# ThirteenV4 Agent Protocols

## 1. Core Mandates (Highest Priority)
- **Proposal & Execution:** Always provide a proposal for actions first. **Wait for explicit "APPROVE" from the user before executing any code changes.**
- **Git Rule:** NEVER suggest, mention, or perform Git actions unless explicitly commanded. When finished, simply say "Implementation complete."
- **Testing:** Always run unit tests to verify changes.
- **Deployment:** Always rebuild Nakama (Go) and refresh Docker containers if the changes need to be applied to the running server.
- **Clean Architecture:** Always separate domain, application, presenter, views, and Nakama interface layers.
- **Code Generation:** Avoid making god classes. Use best practices and documentation.

## 2. Workflow
1.  **Understand:** Analyze context (Global & Local memory).
2.  **Plan:** Create a proposal.
3.  **Approve:** Wait for user approval.
4.  **Execute:** Implement changes.
5.  **Verify:** Run tests and build checks.

## 3. Architecture & Coding Standards (Clean Architecture & DDD)
- **Structure:**
  - `/Server`: Nakama logic (Go).
  - `/Client`: Unity project (C#).
  - `/proto`: Protobuf definitions.
- **Domain Isolation:**
  - **Go:** Core rules in pure Go (`/internal/domain`) with NO Nakama dependencies.
  - **Unity:** Game logic in pure C# classes, decoupled from `MonoBehaviours`.
- **Presentation Layer (Unity):**
  - Use **MVP (Model-View-Presenter)** pattern.
  - **Views:** Handle UI components and user input forwarding only. No business logic.
  - **Presenters:** Handle UI logic, communication with the Application layer, and updating Views.
  - **Dependency Injection:** Use VContainer for all DI.
- **Data Integrity:**
  - **Go:** Authoritative source of truth. Validate all moves.
  - **Unity:** Prediction only. Update UI based on server state (DTOs).

## 4. Project Context: Tien Len (Thirteen)
- **Frontend:** Unity (C#)
- **Backend:** Nakama Server (Go-based distributed modules)
- **Database:** PostgreSQL (via Nakama's storage engine)
- **Architecture:** Hybrid Clean Architecture/DDD.
  - **Source of Truth:** Nakama Go Modules (Server-Authoritative).
  - **Client:** Unity acts as a View/Presenter with local state prediction.

## 5. Nakama + Go Implementation Rules
- **Match Loop:** Implement the `MatchLoop` in Go to handle tick-based state broadcasts.
- **Concurrency:** Use Go channels and mutexes safely. Avoid global variables; maintain state within the Nakama `MatchState` struct.
- **Protocols:** Use JSON or Protobuf for messages. Ensure that any change to the Go message struct is reflected in the Unity C# DTOs immediately.

## 6. Unity (C#) Coding Standards
- **MVP Pattern:** Strictly follow Model-View-Presenter. 
  - **Presenters:** Pure C# classes (no MonoBehaviour) handling logic and state mapping. Name: `[Feature]Presenter`.
  - **Views:** `MonoBehaviours` handling UI references and events. Name: `[Feature]View`.
  - **NO Controllers:** Do not use the suffix "Controller". Refactor existing "Controllers" to Presenters or Views as appropriate.
- **Data-Driven UI:** The UI must react to the `GameState` DTO received from Nakama. Do not couple UI logic directly to input logic.
- **Async/Await:** Use `UniTask` or standard `Task` for all network calls. Do not use blocking calls or coroutines for API requests.
- **Event-Driven:** Use a local `EventBus` to decouple network messages from UI updates (e.g., `OnCardPlayed`, `OnTurnChanged`).

## 7. Specific Game Rules: Tien Len
- **Card Ranking:** 2 (Highest) > A > K > Q > J > 10 > 9 > 8 > 7 > 6 > 5 > 4 > 3 (Lowest).
- **Suit Ranking:** Hearts > Diamonds > Clubs > Spades.
- **Validation:**
  - Valid combinations: Singles, Pairs, Triples, Sequences (3+), Bombs (Four of a Kind or 3+ pairs in sequence).
  - "Chopping": Bombs can defeat 2s.
- **Turn Logic:** Strict turn-based state machine. Passing skips the player until the round resets.

## 8. Instructions for the Agent (Jules)
1. **Context First:** Before writing code, analyze if the change affects the Shared Protocol (Network Messages) or Core Domain logic.
2. **Go Modifications:** When modifying backend logic, ensure `MatchState` is updated transactionally and a broadcast is sent.
3. **Unity Modifications:** When creating a new UI component, ensure it subscribes to the Event Bus and does not hold its own state.
4. Synchronization: If game rules change (e.g., Bomb logic), update **both** the Go validator and the Unity client predictor.

## 9. Error Handling & Navigation
- **Critical Errors:** For game-breaking errors (network loss, fatal logic exception) where the user cannot proceed, use `ErrorContext.ShowError(message)`.
- **Error Scene:** This will load the `ErrorScene` which displays the message and provides a "Back" button to the previous context.
- **UI Feedback:** For non-critical errors (invalid move), use local UI feedback (toast, shake animation) instead of changing scenes.