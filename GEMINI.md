# Gemini Project Context - ThirteenV4

## Rules
- **Proposal & Execution:** Always provide a proposal for actions first. Once accepted, you may execute the file changes (implementation).
- **Git Rule:** NEVER suggest, mention, or perform Git actions unless explicitly commanded. When finished, simply say "Implementation complete."
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