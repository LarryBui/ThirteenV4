# Gemini Project Context - ThirteenV4 (Unified Agent Protocols)

## 1. Core Mandates (Highest Priority)
- **Proposal & Execution:** Always provide a proposal for actions first. **Wait for explicit "APPROVE" from the user before executing any code changes.** (CRITICAL PROTOCOL).
- **Git Rule:** NEVER suggest, mention, or perform Git actions unless explicitly commanded. When finished, simply say "Implementation complete."
- **Testing:** Always run unit tests to verify changes.
- **Deployment:** Always rebuild Nakama (Go) and refresh Docker containers if the changes need to be applied to the running server.
- **Clean Architecture:** Always separate domain, application, presenter, views, and Nakama interface layers.
- **Memory Rule:** Save context memory to `./.idea/project.md`. Do not use the `save_memory` tool for project context.

## 2. Workflow
1.  **Understand:** Analyze context (Global & Local memory). Use tools to explore the codebase.
2.  **Plan:** Create a concise yet clear proposal.
3.  **Approve:** **WAIT for explicit "APPROVE" from the user.**
4.  **Execute:** Implement changes only after approval.
5.  **Verify:** Run tests, build checks, and linting.

## 3. Architecture & Coding Standards (Clean Architecture & DDD)
- **Structure:** `/Server` (Go), `/Client` (Unity), `/proto` (Protobuf).
- **Domain Isolation:** Go rules in `/internal/domain` (pure Go). Unity logic in pure C# classes.
- **Presentation Layer (Unity):** Strictly follow **MVP (Model-View-Presenter)**.
  - **Presenters:** Pure C# handling logic. Name: `[Feature]Presenter`.
  - **Views:** `MonoBehaviours` for UI. Name: `[Feature]View`.
  - **NO Controllers:** Refactor "Controllers" to Presenters or Views.
- **Data Integrity:** Go is the source of truth. Unity handles local prediction only.

## 4. Tech Stack & Game Rules: Tien Len
- **Stack:** Nakama (Go), Unity (C#), Protobuf, PostgreSQL.
- **Card Ranking:** 2 (High) > A > K > Q > J > 10 > 9 > 8 > 7 > 6 > 5 > 4 > 3 (Low).
- **Suit Ranking:** Hearts > Diamonds > Clubs > Spades.
- **Turn Logic:** Strict turn-based machine. Passing skips until round reset.

## 5. Implementation Rules
- **Async/Await:** Use `UniTask` or `Task` for all network calls.
- **Event-Driven:** Use events to decouple layers.
- **Protocols:** Keep Go structs and Unity DTOs synchronized.
- **Error Handling:** Use `ErrorContext.ShowError` for critical issues; Toast for gameplay feedback.
