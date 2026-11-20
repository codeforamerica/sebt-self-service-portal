import { strict as assert } from "node:assert";
import { createServer } from "node:net";
import type { AddressInfo, Server } from "node:net";
import { describe, it } from "node:test";

import { portAvailable } from "./port-available.mjs";

function listen(host: string): Promise<Server> {
  return new Promise((resolve, reject) => {
    const server = createServer();
    server.once("error", reject);
    server.listen(0, host, () => resolve(server));
  });
}

function close(server: Server): Promise<void> {
  return new Promise((resolve) => server.close(() => resolve()));
}

function portOf(server: Server): number {
  return (server.address() as AddressInfo).port;
}

describe("portAvailable", () => {
  it("passes when nothing answers on the port", async () => {
    const server = await listen("127.0.0.1");
    const port = portOf(server);
    await close(server);

    await portAvailable(port, "Stop it.").check();
  });

  for (const host of ["127.0.0.1", "::1"]) {
    it(`throws with the port and the remedy when a process answers on ${host}`, async (t) => {
      const server = await listen(host).catch(() => undefined);
      if (!server) {
        t.skip(`${host} is not available on this machine`);
        return;
      }

      try {
        await assert.rejects(
          async () => portAvailable(portOf(server), "Stop the other portal.").check(),
          (error: Error) =>
            error.message.includes(String(portOf(server))) &&
            error.message.includes("Stop the other portal."),
        );
      } finally {
        await close(server);
      }
    });
  }
});
