// The shape that each infrastructure capability speaks in.
//
// A capability provider does not configure the API. It makes the resources that it owns.
// Then it returns what those resources need from the other parts of the graph. These are
// the settings that the API must get, the resources that the API must wait for, and the
// obligations of the host machine. apphost.mts discharges them at one place. Thus one
// call site assembles the environment of the API. Before this, each module that held a
// reference to the API changed it.
//
// The result is that the configuration of the application becomes data. A program can
// print it and count it. A later change can make a comparison of it with the values that
// appsettings, tofu, and web.config give to the deployed environments.

import type {
  NextJsAppResource,
  ProjectResource,
} from "../.aspire/modules/aspire.mjs";

/**
 * Each value that `withEnvironment` accepts. These are literals, parameters, endpoint
 * properties, connection strings, and reference expressions. This type comes from the
 * generated API, and this module does not write it again. Thus it cannot disagree with
 * the generated API.
 */
export type EnvValue = Parameters<ProjectResource["withEnvironment"]>[1];

/**
 * Each resource that `waitFor` accepts, but only those that can give their own name. Some
 * members of the generated union are abstract and have no name. The inventory below needs
 * a name.
 */
export type WaitTarget = Parameters<ProjectResource["waitFor"]>[0] & {
  getResourceName(): Promise<string>;
};

/** One configuration value that the application needs, or must not have. */
export interface ConfigSetting {
  /** The key in the form of the application, for example `SmtpClientSettings__SmtpPort`. */
  key: string;
  value: EnvValue;
  /** Why the application needs the value. Write one line. */
  why: string;
  /**
   * `supply` gives the application a value. The application cannot start without that
   * value, or it must read the value from a file that each developer edits by hand.
   * `neutralize` clears a value that the application must not have. Usually this is a
   * development shortcut that disagrees with the graph. Both are `withEnvironment` calls,
   * and they look the same in the source. Thus this field records the difference.
   */
  kind?: "supply" | "neutralize";
}

/** One constraint of sequence that the application has on a resource. */
export interface Wait {
  resource: WaitTarget;
  /** `healthy`: the resource runs and its health checks pass. `completion`: a one-shot job that stopped. */
  until: "healthy" | "completion";
}

/**
 * Each resource that can export telemetry. A project and a Next.js application both have
 * these methods, but each returns its own type. Thus this names the methods and not a
 * resource of the generated API.
 */
export interface ExportTarget {
  withOtlpExporter(): PromiseLike<unknown>;
  withBrowserLogs(): PromiseLike<unknown>;
  getResourceName(): Promise<string>;
}

/** One resource that sends its telemetry somewhere. */
export interface Exporter {
  resource: ExportTarget;
  /** Also track the browser that Aspire starts. A front end has one, a server does not. */
  browserLogs?: boolean;
  /** Why the resource exports. Write one line. */
  why: string;
}

/** One obligation of the host machine. The check runs before any resource exists. */
export interface Preflight {
  /** What the machine must have. Write it so that the startup log reads as a checklist. */
  description: string;
  /** This function throws an error with the remedy when the machine does not satisfy the obligation. */
  check(): void | Promise<void>;
}

/** What a capability needs from the other parts of the graph. */
export interface Requirements {
  /** Settings for the API. */
  settings: ConfigSetting[];
  /**
   * Settings for the portal. The AppHost adds the portal after the resources of a state.
   * Thus only a late binding can give these. Read `bindPortal` in ./sign-in.mts.
   */
  portalSettings?: ConfigSetting[];
  /**
   * The resources that must export telemetry. The exporter is a method of the resource
   * and not an environment variable, so it cannot be a setting. It is here so that this
   * wiring stays data that a program can print. Read ./telemetry/contract.mts.
   */
  exporters?: Exporter[];
  waits: Wait[];
}

/** The resources that a capability can configure. */
export interface CapabilityTargets {
  /** The API. Each capability configures it. */
  api: ProjectResource;
  /** The portal. It exists only after the AppHost adds the web applications. */
  portal?: NextJsAppResource;
}

/**
 * Runs the host obligations of a capability. This occurs before the AppHost builds the
 * resource graph. Thus an unsatisfied obligation fails in one second, and it does not
 * fail after the containers start.
 */
export async function runPreflight(
  capability: string,
  checks: readonly Preflight[],
): Promise<void> {
  for (const preflight of checks) {
    await preflight.check();
    console.log(`[preflight] ${capability}: ${preflight.description}`);
  }
}

/**
 * Discharges the requirements of a capability onto the API, and prints what it did.
 *
 * The print is as important as the wiring. It is the one place that can answer this
 * question: which configuration does this state need to run? A person does not have to
 * read each module.
 */
export async function applyRequirements(
  targets: CapabilityTargets,
  capability: string,
  requirements: Requirements,
): Promise<void> {
  for (const setting of requirements.settings) {
    await targets.api.withEnvironment(setting.key, setting.value);
  }

  const portalSettings = requirements.portalSettings ?? [];
  if (portalSettings.length > 0) {
    const portal = targets.portal;
    // The alternative is a setting that goes nowhere, which is the failure mode that this
    // module exists to prevent.
    if (!portal) {
      throw new Error(
        `Capability '${capability}' gives settings for the portal, but the portal does not exist at this point in the graph. Return them from bindPortal instead.`,
      );
    }
    for (const setting of portalSettings) {
      await portal.withEnvironment(setting.key, setting.value);
    }
  }

  for (const exporter of requirements.exporters ?? []) {
    await exporter.resource.withOtlpExporter();
    if (exporter.browserLogs) {
      await exporter.resource.withBrowserLogs();
    }
  }

  for (const wait of requirements.waits) {
    if (wait.until === "completion") {
      await targets.api.waitForCompletion(wait.resource);
    } else {
      await targets.api.waitFor(wait.resource);
    }
  }

  await logRequirements(capability, requirements);
}

async function logRequirements(
  capability: string,
  requirements: Requirements,
): Promise<void> {
  const keysOfKind = (kind: ConfigSetting["kind"]): string[] =>
    requirements.settings
      .filter((setting) => (setting.kind ?? "supply") === kind)
      .map((setting) => setting.key);

  const supplied = keysOfKind("supply");
  if (supplied.length > 0) {
    console.log(`[config] ${capability} supplies api: ${supplied.join(", ")}`);
  }

  const neutralized = keysOfKind("neutralize");
  if (neutralized.length > 0) {
    console.log(
      `[config] ${capability} neutralizes api: ${neutralized.join(", ")}`,
    );
  }

  const portalSettings = requirements.portalSettings ?? [];
  if (portalSettings.length > 0) {
    const keys = portalSettings.map((setting) => setting.key);
    console.log(`[config] ${capability} supplies web: ${keys.join(", ")}`);
  }

  const exporters = requirements.exporters ?? [];
  if (exporters.length > 0) {
    const described = await Promise.all(
      exporters.map(async (exporter) => {
        const name = await exporter.resource.getResourceName();
        return exporter.browserLogs ? `${name} (+browser)` : name;
      }),
    );
    console.log(`[config] ${capability} exports from: ${described.join(", ")}`);
  }

  if (requirements.waits.length > 0) {
    const described = await Promise.all(
      requirements.waits.map(
        async (wait) => `${await wait.resource.getResourceName()} (${wait.until})`,
      ),
    );
    console.log(`[config] ${capability} waits for: ${described.join(", ")}`);
  }
}
