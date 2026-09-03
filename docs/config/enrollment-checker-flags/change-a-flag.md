# Change a Flag

This page shows where a flag value lives, how to edit it, and how long the new value takes
to reach a family.

## Where a value lives

Three layers set a flag value. Each layer replaces the layer above it:

| Order | Source | Notes |
| --- | --- | --- |
| 1 | `appsettings.json` | Base defaults. Sets `enable_enrollment` to `true`. |
| 2 | `appsettings.{state}.json` | The state overlay. Git ignores this file. |
| 3 | The AppSettings profile in AWS AppConfig | What operators edit. |

A layer sets only the values that it changes. The base default gives a safe result to a
state that sets no value.

**Git ignores `appsettings.{state}.json`.** The file `appsettings.{state}.example.json` is
the only record of a state overlay in the repository. A flag that is absent from the example
file has 2 problems. Nobody knows to configure it. The new value also does not ship, because
Git never commits the real file. The pull request checklist includes this step.

## Procedure

1. Open the AppSettings profile for that state and that environment in AWS AppConfig.
2. Set the flag value.
3. Update `appsettings.{state}.example.json` in the same pull request if this value is the
   new default.
4. Wait for the AWS AppConfig deployment to reach 100%.
5. Wait 150 more seconds for the API and the browser polls.
6. Read `GET /api/enrollment/features` to confirm the new value.

The endpoint is public. Use this command for step 6:

```bash
curl -s https://<api-host>/api/enrollment/features | jq
```

## How long a change takes

The checker is a static export. It has no server. A `NEXT_PUBLIC_*` value is fixed at build
time. These 3 flags are the only values that change without a new build.

| Step | Component | Delay |
| --- | --- | --- |
| 1 | AWS AppConfig deploys the new version | the deployment strategy controls this |
| 2 | The AppConfig Agent sidecar reads the new version | the agent default controls this |
| 3 | The API reads the agent cache | 90 seconds |
| 4 | The checker reads `GET /api/enrollment/features` | 60 seconds |

Steps 3 and 4 are the only delays in this repository. Their total is a maximum of 150
seconds. Steps 1 and 2 are AWS settings. This repository does not set them. A gradual
deployment strategy adds minutes to step 1. Ask your infrastructure operator for both
values.

A background tab stops its polls. It reads the endpoint again when the user selects the tab.

## Problems and causes

| Problem | Cause |
| --- | --- |
| A new flag value is not visible. | Confirm that the AWS AppConfig deployment is complete. Then wait 150 more seconds. |
| A state moved to past tense without a request. | AWS AppConfig sets `enable_enrollment` to `false`. Or the base default is absent. An absent flag reads as closed. |
| The apply link is absent, but `enable_apply` is `true`. | The build has no `NEXT_PUBLIC_APPLICATION_URL`. Or the season is closed. |
| The income eligibility tool is absent, but its flag is `true`. | `EnrollmentChecker:IncomeEligibility` is absent or zero. |
| A value ignores AWS AppConfig. | The code reads `IOptions<T>`. Change it to `IOptionsMonitor<T>.CurrentValue`. |
