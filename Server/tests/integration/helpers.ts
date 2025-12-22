import { Client, Session, Socket } from "@heroiclabs/nakama-js";

export const CONFIG = {
    host: "127.0.0.1",
    port: "7350",
    serverKey: "defaultkey",
    useSSL: false,
};

export class TestUser {
    client: Client;
    session: Session;
    socket: Socket;
    userId: string;
    username: string;
    private matchDataListeners: ((data: any) => void)[] = [];

    constructor(client: Client, session: Session, socket: Socket, username: string) {
        this.client = client;
        this.session = session;
        this.socket = socket;
        this.userId = session.user_id!;
        this.username = username;

        this.socket.onmatchdata = (matchData) => {
            this.matchDataListeners.forEach(l => l(matchData));
        };
    }

    waitForOpCode(opCode: number, timeoutMs: number = 5000, predicate?: (data: any) => boolean): Promise<any> {
        return new Promise((resolve, reject) => {
            const timer = setTimeout(() => {
                this.matchDataListeners = this.matchDataListeners.filter(l => l !== listener);
                reject(new Error(`User ${this.username} timed out waiting for OpCode ${opCode}`));
            }, timeoutMs);

            const listener = (data: any) => {
                if (data.op_code === opCode) {
                    if (predicate && !predicate(data.data)) {
                        return; // Keep waiting
                    }
                    clearTimeout(timer);
                    this.matchDataListeners = this.matchDataListeners.filter(l => l !== listener);
                    resolve(data.data);
                }
            };

            this.matchDataListeners.push(listener);
        });
    }

    async joinMatch(matchId: string): Promise<string> {
        const match = await this.socket.joinMatch(matchId);
        return match.match_id;
    }

    async close() {
        if (this.socket) {
            await this.socket.disconnect(false);
        }
    }
}

export async function createTestUser(customId?: string): Promise<TestUser> {
    const client = new Client(CONFIG.serverKey, CONFIG.host, CONFIG.port, CONFIG.useSSL);
    const id = customId || `test_user_${Date.now()}_${Math.floor(Math.random() * 1000)}`;
    
    const session = await client.authenticateDevice(id, true, id);
    
    const socket = client.createSocket(CONFIG.useSSL, false);
    await socket.connect(session, true);

    return new TestUser(client, session, socket, id);
}
