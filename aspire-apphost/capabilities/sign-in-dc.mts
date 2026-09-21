// DC signs a guardian in with an email OTP. The API makes the code and does a check of
// it. Thus the flow needs one item of infrastructure only: a destination for the mail.

import { EndpointProperty } from "../.aspire/modules/aspire.mjs";
import type {
  SignInCapability,
  SignInContext,
  SignInProvider,
} from "./sign-in.mjs";

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
      ],
      waits: [{ resource: mailpit, until: "healthy" }],
    },
  };
}

export const dcEmailOtp: SignInProvider = {
  name: "email OTP via Mailpit",
  signInHint:
    "OTP codes arrive in the Mailpit inbox, reachable from the mailpit resource in the dashboard.",
  // Mailpit uses a fixed image, and it needs nothing from the host machine. Each other
  // resource already needs the container runtime. This list is empty by design, and not
  // by omission.
  preflight: () => [],
  provision,
};
