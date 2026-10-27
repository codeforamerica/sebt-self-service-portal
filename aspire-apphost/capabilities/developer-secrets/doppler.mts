// Loads local development secrets from Doppler. It calls the CLI with no shell, so it
// behaves the same on Windows, and avoids the generated SDK so its tests run without Aspire.

import { spawnSync } from "node:child_process";

/** What one call to the Doppler CLI gave back. */
export type DopplerRun =
  | { outcome: "ok"; stdout: string }
  | { outcome: "missing" }
  | { outcome: "failed"; stderr: string };

/** Runs the Doppler CLI with these arguments. */
export type RunDoppler = (args: readonly string[]) => DopplerRun;

/** Where the AppHost gets its secrets for this start. */
export type DeveloperSecrets =
  | { source: "doppler"; project: string; config: string; values: ReadonlyMap<string, string> }
  | { source: "local-files"; reason: "disabled" }
  | { source: "local-files"; reason: "not-set-up"; expectedProject: string };

export interface LoadDeveloperSecretsOptions {
  /** False when USE_DOPPLER turns Doppler off. */
  useDoppler: boolean;
  expectedProject: string;
  /** The only Doppler environment whose configs may be loaded. */
  expectedEnvironment: string;
  run: RunDoppler;
}

const configLocation = "aspire-apphost/config.mts";
const dopplerMetadataKeys = new Set(["DOPPLER_PROJECT", "DOPPLER_CONFIG", "DOPPLER_ENVIRONMENT"]);

/** Runs the real Doppler CLI from `cwd`, so it reads the doppler.yaml scope of the repo. */
export function runDopplerCli(cwd: string): RunDoppler {
  return (args) => {
    const result = spawnSync("doppler", args, { cwd, encoding: "utf8" });
    if ((result.error as NodeJS.ErrnoException | undefined)?.code === "ENOENT") {
      return { outcome: "missing" };
    }
    if (result.error || result.status !== 0) {
      return { outcome: "failed", stderr: result.stderr || String(result.error ?? "") };
    }
    return { outcome: "ok", stdout: result.stdout };
  };
}

/**
 * Decides where secrets come from, and loads them from Doppler when it is set up.
 *
 * It throws when Doppler is set up but cannot give secrets from the expected environment,
 * so the AppHost never starts with secrets that are missing or that belong to a deployed
 * environment. No error includes a secret value.
 */
export function loadDeveloperSecrets(options: LoadDeveloperSecretsOptions): DeveloperSecrets {
  const { useDoppler, expectedProject, expectedEnvironment, run } = options;

  if (!useDoppler) {
    return { source: "local-files", reason: "disabled" };
  }

  const project = run(["configure", "get", "project", "--plain"]);
  if (project.outcome !== "ok" || project.stdout.trim() !== expectedProject) {
    return { source: "local-files", reason: "not-set-up", expectedProject };
  }

  const download = run(["secrets", "download", "--no-file", "--format", "json"]);
  if (download.outcome !== "ok") {
    const detail = download.outcome === "failed" ? download.stderr.trim() : "the Doppler CLI was not found";
    throw new Error(
      `Doppler couldn't load secrets: ${detail}\n` +
        "Fix the Doppler error above, or set USE_DOPPLER=false to use local config files.",
    );
  }

  const secrets = parseDownload(download.stdout);
  const environment = secrets.DOPPLER_ENVIRONMENT;
  const config = secrets.DOPPLER_CONFIG ?? "unknown";
  if (environment !== expectedEnvironment) {
    throw new Error(
      `Doppler is set up with config '${config}', but the AppHost loads only configs in the '${expectedEnvironment}' environment, ` +
        "because other configs hold the keys of deployed environments. " +
        `Run \`doppler setup\` from the repo root and pick '${expectedEnvironment}' or your personal config. ` +
        `The expected project and environment are set in ${configLocation}.`,
    );
  }

  const values = new Map(
    Object.entries(secrets).filter(([key]) => !dopplerMetadataKeys.has(key)),
  );
  return { source: "doppler", project: expectedProject, config, values };
}

function parseDownload(stdout: string): Record<string, string> {
  try {
    return JSON.parse(stdout) as Record<string, string>;
  } catch {
    // The output holds secret values, so the error must not include it.
    throw new Error(
      "Doppler returned secrets in a format the AppHost could not read. Run `doppler secrets download --no-file --format json` to check your Doppler CLI, or set USE_DOPPLER=false.",
    );
  }
}

/** One line for the startup log that says where the secrets come from. */
export function describeDeveloperSecrets(secrets: DeveloperSecrets): string {
  if (secrets.source === "doppler") {
    return `Using Doppler: ${secrets.project} / ${secrets.config}. Doppler values override matching keys in local config files.`;
  }
  if (secrets.reason === "disabled") {
    return "USE_DOPPLER=false: using local config files.";
  }
  return `Doppler isn't set up for this repo (expected project '${secrets.expectedProject}', set in ${configLocation}): using local config files.`;
}
