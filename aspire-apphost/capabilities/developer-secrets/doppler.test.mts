import { strict as assert } from "node:assert";
import { describe, it } from "node:test";

import {
  describeDeveloperSecrets,
  loadDeveloperSecrets,
} from "./doppler.mjs";
import type { DopplerRun, RunDoppler } from "./doppler.mjs";

const project = "safety-net-sebt-self-service-portal";
const projectArgs = "configure get project --plain";
const downloadArgs = "secrets download --no-file --format json";

/** A stand-in for the Doppler CLI that answers each command and records what was asked. */
function fakeDoppler(answers: Record<string, DopplerRun>): RunDoppler & { calls: string[] } {
  const calls: string[] = [];
  const run = (args: readonly string[]): DopplerRun => {
    const command = args.join(" ");
    calls.push(command);
    const answer = answers[command];
    assert.ok(answer, `unexpected Doppler command: ${command}`);
    return answer;
  };
  return Object.assign(run, { calls });
}

function download(secrets: Record<string, string>): DopplerRun {
  return { outcome: "ok", stdout: JSON.stringify(secrets) };
}

function load(run: RunDoppler, useDoppler = true) {
  return loadDeveloperSecrets({
    useDoppler,
    expectedProject: project,
    expectedEnvironment: "local",
    run,
  });
}

describe("loadDeveloperSecrets", () => {
  it("uses local files without calling Doppler when USE_DOPPLER turns it off", () => {
    const run = fakeDoppler({});
    assert.deepEqual(load(run, false), { source: "local-files", reason: "disabled" });
    assert.deepEqual(run.calls, []);
  });

  const notSetUp: Record<string, DopplerRun> = {
    "Doppler is not installed": { outcome: "missing" },
    "the project lookup fails": { outcome: "failed", stderr: "no config" },
    "the repo is set up for another project": { outcome: "ok", stdout: "some-other-project\n" },
    "the repo has no project": { outcome: "ok", stdout: "" },
  };
  for (const [situation, answer] of Object.entries(notSetUp)) {
    it(`uses local files when ${situation}`, () => {
      const run = fakeDoppler({ [projectArgs]: answer });
      assert.deepEqual(load(run), {
        source: "local-files",
        reason: "not-set-up",
        expectedProject: project,
      });
      assert.deepEqual(run.calls, [projectArgs]);
    });
  }

  it("matches the project when Doppler prints a trailing newline", () => {
    const run = fakeDoppler({
      [projectArgs]: { outcome: "ok", stdout: `${project}\n` },
      [downloadArgs]: download({
        DOPPLER_PROJECT: project,
        DOPPLER_ENVIRONMENT: "local",
        DOPPLER_CONFIG: "local_personal",
      }),
    });
    assert.equal(load(run).source, "doppler");
  });

  it("stops with Doppler's error and the opt-out when secrets can't load", () => {
    const run = fakeDoppler({
      [projectArgs]: { outcome: "ok", stdout: project },
      [downloadArgs]: { outcome: "failed", stderr: "Doppler Error: Unable to fetch secrets" },
    });
    assert.throws(
      () => load(run),
      (error: Error) =>
        /Unable to fetch secrets/.test(error.message) && /USE_DOPPLER=false/.test(error.message),
    );
  });

  it("stops without revealing secrets when the config is outside the local environment", () => {
    const run = fakeDoppler({
      [projectArgs]: { outcome: "ok", stdout: project },
      [downloadArgs]: download({
        DOPPLER_PROJECT: project,
        DOPPLER_ENVIRONMENT: "dev",
        DOPPLER_CONFIG: "dev_co_state_secrets",
        JWTSETTINGS__SECRETKEY: "s3cr3t-value",
      }),
    });
    assert.throws(
      () => load(run),
      (error: Error) =>
        /dev_co_state_secrets/.test(error.message) &&
        /'local' environment/.test(error.message) &&
        /doppler setup/.test(error.message) &&
        /aspire-apphost\/config\.mts/.test(error.message) &&
        !error.message.includes("s3cr3t-value"),
    );
  });

  it("stops without revealing Doppler's output when it isn't JSON", () => {
    const run = fakeDoppler({
      [projectArgs]: { outcome: "ok", stdout: project },
      [downloadArgs]: { outcome: "ok", stdout: "not json s3cr3t-value" },
    });
    assert.throws(
      () => load(run),
      (error: Error) => !error.message.includes("s3cr3t-value"),
    );
  });

  it("returns the secrets without Doppler's own metadata", () => {
    const run = fakeDoppler({
      [projectArgs]: { outcome: "ok", stdout: project },
      [downloadArgs]: download({
        DOPPLER_PROJECT: project,
        DOPPLER_ENVIRONMENT: "local",
        DOPPLER_CONFIG: "local_personal",
        SMARTY__AUTHID: "smarty-id",
        SOCURE__APIKEY: "socure-key",
      }),
    });
    const secrets = load(run);
    assert.equal(secrets.source, "doppler");
    if (secrets.source !== "doppler") {
      return;
    }
    assert.equal(secrets.project, project);
    assert.equal(secrets.config, "local_personal");
    assert.deepEqual(
      [...secrets.values.entries()].sort(),
      [
        ["SMARTY__AUTHID", "smarty-id"],
        ["SOCURE__APIKEY", "socure-key"],
      ],
    );
  });
});

describe("describeDeveloperSecrets", () => {
  it("names the project and config in Doppler mode", () => {
    assert.equal(
      describeDeveloperSecrets({
        source: "doppler",
        project,
        config: "local_personal",
        values: new Map(),
      }),
      `Using Doppler: ${project} / local_personal. Doppler values override matching keys in local config files.`,
    );
  });

  it("says USE_DOPPLER turned Doppler off", () => {
    assert.equal(
      describeDeveloperSecrets({ source: "local-files", reason: "disabled" }),
      "USE_DOPPLER=false: using local config files.",
    );
  });

  it("says Doppler isn't set up and where the expected project is named", () => {
    const message = describeDeveloperSecrets({
      source: "local-files",
      reason: "not-set-up",
      expectedProject: project,
    });
    assert.match(message, /isn't set up for this repo/);
    assert.match(message, /aspire-apphost\/config\.mts/);
    assert.ok(message.includes(project));
  });
});
