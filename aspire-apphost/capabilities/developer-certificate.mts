// A check of the trusted developer certificate.
//
// This is silent failure 2 in docs/adr/0022-aspire-local-dev-orchestrator.md. Aspire
// gives a container a developer certificate and then serves it with TLS. Without a
// trusted certificate, Aspire serves the plain endpoint and it shows no message. Redis
// then answers on a port that the API does not use, and Keycloak answers nothing at the
// address that holds the OIDC discovery document.
//
// The certificate is in the keychain of the machine, so a check of a file cannot find
// it. `dotnet dev-certs https --check --trust` gives exit code 0 when a trusted
// certificate exists. That is the same certificate that Aspire mounts: the thumbprint
// in the output agrees with the file name in KC_HTTPS_CERTIFICATE_FILE.
//
// Two capabilities of CO need this, and both include the check. The result is held
// after the first run, so the command runs one time for each start of the AppHost.

import { execFileSync } from "node:child_process";

import type { Preflight } from "./requirements.mjs";

/** The result of the first check. `undefined` means that no check has run. */
let trusted: boolean | undefined;

function hasTrustedCertificate(): boolean {
  if (trusted !== undefined) {
    return trusted;
  }

  try {
    execFileSync("dotnet", ["dev-certs", "https", "--check", "--trust"], {
      stdio: "ignore",
    });
    trusted = true;
  } catch {
    // A non-zero exit code means that no trusted certificate is present. An absent
    // `dotnet` gives the same result here, and the API needs `dotnet` too, so the
    // message below is correct in both cases.
    trusted = false;
  }

  return trusted;
}

/**
 * Makes a check that the machine has a trusted developer certificate.
 *
 * Aspire needs a person for `aspire certs trust`, because macOS shows a prompt for the
 * keychain. Thus this check reports the problem, and it does not correct it.
 */
export const trustedDeveloperCertificate: Preflight = {
  description: "a trusted HTTPS developer certificate",
  check: () => {
    if (!hasTrustedCertificate()) {
      throw new Error(
        "No trusted HTTPS developer certificate. Run `aspire certs trust` one time on this machine. " +
          "Without it Aspire serves Redis and Keycloak with no TLS, it shows no message, and the API cannot reach either one.",
      );
    }
  },
};
