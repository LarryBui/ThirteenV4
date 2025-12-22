import { createTestUser, TestUser, waitForMatchState } from "./helpers";

const OP_CODE_START_GAME = 1;
const OP_CODE_GAME_STARTED = 100; // Server -> Client event

describe("Tien Len Integration Tests", () => {
    let users: TestUser[] = [];

    afterEach(async () => {
        // Cleanup all users
        await Promise.all(users.map(u => u.close()));
        users = [];
    });

    test("Full Game Start Flow (4 Players)", async () => {
        // 1. Create 4 Users
        console.log("Creating 4 users...");
        for (let i = 0; i < 4; i++) {
            users.push(await createTestUser());
        }

        const owner = users[0];
        console.log(`User 0 (${owner.userId}) initialized.`);

        // 2. Owner Finds/Creates Match via RPC (Required for Server Authoritative)
        console.log("Owner calling find_match...");
        // Payload must be an object for the JS client, even if server treats it as raw bytes/string often.
        // Or if TS insists on object, we give it object.
        const rpcResult = await owner.client.rpc(owner.session, "find_match", {});
        
        // Nakama JS automatically parses JSON payload if content-type matches or it looks like JSON.
        // Server returns {"match_id": "..."}
        const payload = rpcResult.payload as any;
        const finalMatchId = payload.match_id;
        
        if (!finalMatchId) {
            throw new Error(`RPC find_match returned invalid payload: ${JSON.stringify(payload)}`);
        }
        
        console.log(`Match created/found: ${finalMatchId}`);
        
        // Owner must also join the match returned by RPC (RPC just creates it, doesn't join)
        await owner.socket.joinMatch(finalMatchId);
        console.log("Owner joined match.");

        // 3. Other players join
        for (let i = 1; i < 4; i++) {
            await users[i].socket.joinMatch(finalMatchId);
            console.log(`User ${i} joined match.`);
        }

        // Wait a moment for presences to sync
        await new Promise(r => setTimeout(r, 1000));

        // 4. Setup Listeners for GameStarted
        const startPromises = users.map((u, index) => {
            return new Promise<void>((resolve, reject) => {
                const timer = setTimeout(() => reject(new Error(`User ${index} did not receive GameStarted`)), 5000);
                u.socket.onmatchdata = (msg) => {
                    if (msg.op_code === OP_CODE_GAME_STARTED) {
                        clearTimeout(timer);
                        // Optional: Decode proto if we had it. 
                        // For now just verifying we got the opcode is enough proof logic triggered.
                        console.log(`User ${index} received GameStarted!`);
                        resolve();
                    }
                };
            });
        });

        // 5. Owner starts game
        console.log("Owner sending StartGame...");
        // Payload should be a protobuf message. 
        // Since we don't have proto generated in JS, we can send an empty byte array 
        // IF the server handles empty payload gracefully (it should).
        // The server code: proto.Unmarshal(msg.GetData(), request) -> if error return.
        // Empty byte array usually unmarshals to empty object, which is valid.
        await owner.socket.sendMatchState(finalMatchId, OP_CODE_START_GAME, new Uint8Array(0));

        // 6. Assert everyone got the event
        await Promise.all(startPromises);
        console.log("All players verified Game Started.");
    }, 15000); // 15s timeout
});
