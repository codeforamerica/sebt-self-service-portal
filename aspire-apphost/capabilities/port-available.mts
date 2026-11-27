// A check that a fixed host port is free before the AppHost asks Aspire for it.
//
// This guards silent failure 1 in docs/adr/0035-aspire-local-dev-orchestrator.md: when
// another process holds the port, Aspire can report the resource healthy while the other
// process answers.
//
// The check connects, and it does not listen. On macOS a listen on 127.0.0.1 can succeed
// while another process holds the wildcard address, so only a connection shows what the
// browser would reach.

import { connect } from "node:net";

import type { Preflight } from "./requirements.mjs";

/** The loopback addresses that `localhost` resolves to, and that Aspire's proxy binds. */
const loopbackHosts = ["127.0.0.1", "::1"];

const probeTimeoutMs = 1000;

function answers(host: string, port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host, port, timeout: probeTimeoutMs });
    const finish = (inUse: boolean): void => {
      socket.destroy();
      resolve(inUse);
    };
    socket.once("connect", () => finish(true));
    // A refusal, a timeout, or an absent IPv6 stack all mean that nothing answers there.
    socket.once("error", () => finish(false));
    socket.once("timeout", () => finish(false));
  });
}

/**
 * Makes a check that nothing answers on `localhost:<port>`.
 *
 * @param port The host port that a resource needs.
 * @param remedy What a developer does when the port is busy. It ends the error message.
 */
export function portAvailable(port: number, remedy: string): Preflight {
  return {
    description: `port ${port} is free`,
    check: async () => {
      const results = await Promise.all(loopbackHosts.map((host) => answers(host, port)));
      if (results.some((inUse) => inUse)) {
        throw new Error(`Port ${port} is in use on localhost. ${remedy}`);
      }
    },
  };
}
