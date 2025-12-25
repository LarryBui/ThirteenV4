# Gemini Project Context - ThirteenV4

## Rules
- Always provide a proposal for actions first. Once the proposal is explicitly accepted by the user, permission is granted to automatically execute the planned actions.
- Do not automatically commit or push to GitHub without explicit permission.
- Add code comments sparingly, focusing on "why" rather than "what".
- **Testing:** Always run unit tests to verify changes.
- **Deployment:** Always rebuild Nakama (Go) and refresh Docker containers if the changes need to be applied to the running server.
- **Documentation:** Always add documentation to code.
- **Strict Approval:** Do not change code until explicitly approved. Always as for approval before delete files

## Tech Stack
- **Server:** Nakama (Go)
- **Client:** Unity (C#)
- **Protocol:** Protobuf

## Project Structure
- `/Server`: Nakama server logic, domain rules, and app services.
- `/Client`: Unity project files.
- `/proto`: Protobuf definitions.
- `/scripts`: Utility scripts for proto generation.