# Gemini Project Context - ThirteenV4

## Rules
- Always provide a proposal for actions first. Once the proposal is explicitly accepted by the user, permission is granted to automatically execute the planned actions.
- Always ask for permission to change code first.
- Do not automatically commit or push to GitHub without explicit permission.
- Mimic existing project style, structure, and naming conventions.
- Add code comments sparingly, focusing on "why" rather than "what".

## Tech Stack
- **Server:** Nakama (Go)
- **Client:** Unity (C#)
- **Protocol:** Protobuf

## Project Structure
- `/Server`: Nakama server logic, domain rules, and app services.
- `/Client`: Unity project files.
- `/proto`: Protobuf definitions.
- `/scripts`: Utility scripts for proto generation.
