# Gemini Project Context - ThirteenV4

## 1. Core Mandates (Highest Priority)
- **Proposal & Execution:** Always provide a proposal for actions first. **Wait for explicit "APPROVE" from the user before executing any code changes.**
- **Git Rule:** NEVER suggest, mention, or perform Git actions unless explicitly commanded. When finished, simply say "Implementation complete."
- **Testing:** Always run unit tests to verify changes.
- **Deployment:** Always rebuild Nakama (Go) and refresh Docker containers if the changes need to be applied to the running server.

## 2. Architecture & Coding Standards (Clean Architecture & DDD)
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

## 3. Workflow
1.  **Understand:** Analyze context (Global & Local memory).
2.  **Plan:** Create a proposal.
3.  **Approve:** Wait for user approval.
4.  **Execute:** Implement changes.
5.  **Verify:** Run tests and build checks.

## 4. Tech Stack
- **Server:** Nakama (Go)
- **Client:** Unity (C#)
- **Protocol:** Protobuf
