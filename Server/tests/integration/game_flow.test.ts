import { createTestUser, TestUser } from "./helpers";
import * as protobuf from "protobufjs";
import * as path from "path";

const OP_CODE_START_GAME = 1;
const OP_CODE_GAME_STARTED = 100; // Server -> Client event

describe("Tien Len Integration Tests", () => {
    let users: TestUser[] = [];
    let FindMatchResponse: protobuf.Type;

    beforeAll(async () => {
        const root = await protobuf.load(path.join(__dirname, "../../../proto/tienlen.proto"));
        FindMatchResponse = root.lookupType("tienlen.v1.FindMatchResponse");
    });

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

        // 2. Owner Finds/Creates Match via Test RPC
        console.log("Owner calling test_create_match...");
        const rpcResult = await owner.client.rpc(owner.session, "test_create_match", {});
        
        // Nakama JS automatically parses the quoted string from server into a plain string.
        const finalMatchId = rpcResult.payload as unknown as string;
        
        if (!finalMatchId) {
            throw new Error(`RPC test_create_match returned empty matchId`);
        }
        
        console.log(`Match created: ${finalMatchId}`);
        
        // Owner must join
        await owner.socket.joinMatch(finalMatchId);
        console.log("Owner joined match.");

        // 3. Other players join
        for (let i = 1; i < 4; i++) {
            await users[i].socket.joinMatch(finalMatchId);
            console.log(`User ${i} joined match.`);
        }

        // Wait a bit for presences to sync
        await new Promise(r => setTimeout(r, 1000));

        // 4. Setup Listeners for GameStarted
        const startPromises = users.map(u => u.waitForOpCode(OP_CODE_GAME_STARTED));

        // 5. Owner starts game
        console.log("Owner sending StartGame...");
        await owner.socket.sendMatchState(finalMatchId, OP_CODE_START_GAME, new Uint8Array(0));

        // 6. Assert everyone got the event
        await Promise.all(startPromises);
        console.log("All players verified Game Started.");
    }, 15000); // 15s timeout
});
