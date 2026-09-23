// The telemetry capability: which resources export, and where the signals go.
//
// This capability differs from the other 4 in 2 ways.
//
// The 2 states answer it the same way, so there is one provider and the map names it
// twice. `Otel` is declared in appsettings.json only, and neither state example file
// changes it. Split this directory into dc.mts and co.mts when one does.
//
// It also provisions after the web applications exist, because all 3 applications
// export. Thus the context holds the resources. The provider still changes none of them:
// it names them in `exporters`, and applyRequirements calls the exporter.

import type {
  NextJsAppResource,
  ProjectResource,
} from "../../.aspire/modules/aspire.mjs";
import type { AppHostConfig, SupportedState } from "../../config.mjs";
import type { Preflight, Requirements } from "../requirements.mjs";
import { aspireDashboard } from "./dashboard.mjs";

/**
 * What a provider gets. There is no builder, because the dashboard is part of Aspire and
 * this capability adds no resource of its own.
 */
export interface TelemetryContext {
  config: AppHostConfig;
  api: ProjectResource;
  /** The portal. */
  web: NextJsAppResource;
  /** The public enrollment checker. */
  checker: NextJsAppResource;
}

export interface TelemetryCapability {
  requirements: Requirements;
}

export interface TelemetryProvider {
  /** Where the telemetry of this state goes. This reads as a row of the capability matrix. */
  readonly name: string;
  /** What a developer opens to look at a trace or a log. */
  readonly telemetryHint: string;
  /** The obligations of the host machine. The checks run before any resource exists. */
  readonly preflight: (config: AppHostConfig) => readonly Preflight[];
  provision(context: TelemetryContext): Promise<TelemetryCapability>;
}

// A new state in SupportedState is an error of compilation here. One that exports to the
// dashboard names `aspireDashboard`, as these 2 do.
const providers: Record<SupportedState, TelemetryProvider> = {
  dc: aspireDashboard,
  co: aspireDashboard,
};

export function telemetryProviderFor(state: SupportedState): TelemetryProvider {
  return providers[state];
}
