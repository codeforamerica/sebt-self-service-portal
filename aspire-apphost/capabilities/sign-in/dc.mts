// DC signs a guardian in with an email OTP. The API makes the code and does a check of
// it. Thus the flow needs one item of infrastructure only: a destination for the mail.
//
// DC also asks a guardian to prove their identity after sign-in, through Socure. On a
// local machine the stub client answers, so that step needs no infrastructure, only one
// setting that turns it on.

import { EndpointProperty } from "../../.aspire/modules/aspire.mjs";
import type {
  SignInCapability,
  SignInContext,
  SignInProvider,
} from "./contract.mjs";

async function provision({ builder }: SignInContext): Promise<SignInCapability> {
  // The paths of the health checks agree with the MailPit integration of
  // CommunityToolkit. The image tag is fixed. Compose uses the tag `latest`, which
  // changes.
  const mailpit = await builder
    .addContainer("mailpit", { image: "axllent/mailpit", tag: "v1.30.7" })
    .withEndpoint({ name: "smtp", targetPort: 1025, scheme: "smtp" })
    .withHttpEndpoint({ name: "http", targetPort: 8025 })
    .withEnvironment("MP_MAX_MESSAGES", "5000")
    .withHttpHealthCheck({
      path: "/livez",
      statusCode: 200,
      endpointName: "http",
    })
    .withHttpHealthCheck({
      path: "/readyz",
      statusCode: 200,
      endpointName: "http",
    });

  const smtpEndpoint = await mailpit.getEndpoint("smtp");

  return {
    requirements: {
      // The host and the port are separate, because SmtpClientSettings needs them in
      // that form. Aspire also allocates the port, so neither side can hold a fixed
      // value.
      settings: [
        {
          key: "SmtpClientSettings__SmtpServer",
          value: await smtpEndpoint.property(EndpointProperty.Host),
          why: "Where the API sends the OTP mail that a guardian signs in with.",
        },
        {
          key: "SmtpClientSettings__SmtpPort",
          value: await smtpEndpoint.property(EndpointProperty.Port),
          why: "Aspire allocates this port, so the API cannot use the 1025 of Mailpit.",
        },
        // Socure:Enabled has no default in the API. Without this value the API registers
        // DisabledSocureClient, and each call of ID proofing answers NOT_CONFIGURED.
        // Socure:UseStub defaults to true in SocureSettings, so this value alone selects
        // the stub. A developer who holds sandbox credentials in user secrets with
        // UseStub=false still gets the real client, because this supplies Enabled only.
        {
          key: "Socure__Enabled",
          value: "true",
          why: "Turns on ID proofing. The stub client answers, because UseStub defaults to true in the API.",
        },
      ],
      waits: [{ resource: mailpit, until: "healthy" }],
    },
  };
}

export const dcEmailOtp: SignInProvider = {
  name: "email OTP via Mailpit, ID proofing via the Socure stub",
  signInHint:
    "OTP codes arrive in the Mailpit inbox, reachable from the mailpit resource in the dashboard. ID proofing runs against StubSocureClient, so no Socure credentials are needed.",
  // Mailpit uses a fixed image, and it needs nothing from the host machine. Each other
  // resource already needs the container runtime. This list is empty by design, and not
  // by omission.
  preflight: () => [],
  provision,
};
