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

    constructor(client: Client, session: Session, socket: Socket, username: string) {
        this.client = client;
        this.session = session;
        this.socket = socket;
        this.userId = session.user_id!;
        this.username = username;
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
    
    const session = await client.authenticateDevice(id, true, id); // Use ID as username for simplicity
    
    const socket = client.createSocket(CONFIG.useSSL, false);
    await socket.connect(session, true);

    return new TestUser(client, session, socket, id);
}

export function waitForMatchState(socket: Socket, opCode: number, timeoutMs: number = 5000): Promise<any> {
    return new Promise((resolve, reject) => {
        const timer = setTimeout(() => {
            reject(new Error(`Timeout waiting for OpCode ${opCode}`));
        }, timeoutMs);

        socket.onmatchdata = (matchState) => {
            if (matchState.op_code === opCode) {
                clearTimeout(timer);
                resolve(matchState.data);
            }
        };
    });
}
