// The sign-in capability. A guardian shows who they are, and that decision has effects
// on the other parts of the graph.
//
// DC and CO answer this question at different layers. The API of DC makes an email OTP
// and does a check of it. Thus DC needs only a transport for mail. CO gives identity to
// an external provider. Thus CO needs an IdP and a client registration.
//
// The contract is therefore the same in kind, but not in content. Each provider returns
// settings, waits, and preflight checks. Each provider decides what goes in them. A
// common shape such as `{ host, port }` or `{ issuer, clientId }` would be a fiction,
// and the third state would break it.
//
// A provider does not change the API. It returns what it needs, and apphost.mts
// discharges that. Read ./requirements.mts.

import type {
  DistributedApplicationBuilder,
  NextJsAppResource,
} from "../../.aspire/modules/aspire.mjs";
import type { AppHostConfig, SupportedState } from "../../config.mjs";
import type { Preflight, Requirements } from "../requirements.mjs";
import { coKeycloakOidc } from "./co.mjs";
import { dcEmailOtp } from "./dc.mjs";

/**
 * What a provider gets. This is small, and that is intentional. A provider that needs
 * more than the builder and the resolved configuration is a provider that goes outside
 * its own capability.
 */
export interface SignInContext {
  builder: DistributedApplicationBuilder;
  config: AppHostConfig;
}

export interface SignInCapability {
  requirements: Requirements;
  /**
   * The requirements that need the endpoint of the portal. The AppHost adds the portal
   * after it adds the resources of the state. Thus these requirements come later.
   *
   * This function is optional, because DC has none of these requirements. An SMTP sink
   * does not need the address of the portal. A realm with redirect URIs needs it.
   */
  bindPortal?(web: NextJsAppResource): Promise<Requirements>;
}

export interface SignInProvider {
  /** How this state signs a guardian in. This reads as a row of the capability matrix. */
  readonly name: string;
  /** What a developer does to sign in on a local machine. This is the first question that a new person asks. */
  readonly signInHint: string;
  /**
   * The obligations of the host machine. The checks run before any resource exists.
   *
   * This is a function of the configuration, and not a fixed list. The household source
   * capability needs this shape, because the paths of DC come from DC_CONNECTOR_PATH,
   * and only the resolved configuration knows that value. The 2 capabilities keep one
   * shape.
   */
  readonly preflight: (config: AppHostConfig) => readonly Preflight[];
  provision(context: SignInContext): Promise<SignInCapability>;
}

// The map has a key for each state, and the AppHost does not use a switch. Thus a new
// state in SupportedState is an error of compilation here, until that state has a
// sign-in flow.
const providers: Record<SupportedState, SignInProvider> = {
  dc: dcEmailOtp,
  co: coKeycloakOidc,
};

export function signInProviderFor(state: SupportedState): SignInProvider {
  return providers[state];
}
