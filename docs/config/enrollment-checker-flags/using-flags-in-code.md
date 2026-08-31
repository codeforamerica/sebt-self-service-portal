# Use Flags in Code

To set a flag is simple. To use a flag correctly is more difficult. Each consumer must
decide what the checker shows before the values arrive, and if the values never arrive.

## Read a flag through a hook

Do not fetch the features endpoint directly. Do not read `data.someFlag` at a call site. Use
a hook. Each hook holds the failure policy for its flag.

```ts
useCheckerFeatures(apiBaseUrl)   // one shared poll for all consumers
  ├── useEnrollmentSeason()      // → { season: 'open' | 'closed', isResolving }
  └── useApplyHref()             // → string | null
```

The hook `useApplyHref` shows the value of this rule. It returns a URL only if 2 conditions
are true. The flag `enable_apply` must be `true`. A destination must exist. A caller then
writes `{applyHref && <ApplyBlock />}`. No caller repeats the 2 conditions.

## Failure policy

Each consumer has the same question. What does the checker show if the poll fails? The
correct behavior is different for each consumer, because the risk is different in each
direction.

| Consumer | Behavior after a failure | Reason |
| --- | --- | --- |
| `useEnrollmentSeason` | Show present tense. | One failed request must not tell families that enrollment ended. |
| `useApplyHref` | Show no link. | A hidden link is better than a dead link after the window closes. |
| `EligibilityAccordion` | Remove the tool. | The tool gives every household the same result if it has no figures. |

Choose the behavior for each new flag. Write the reason into the hook. A hidden feature is
not always the safe result. The season is the example: hidden present-tense text tells
families that enrollment ended.

## Deployment order

The checker and the API deploy separately. Make each new field in the features schema
optional:

```ts
// An absent field reads as closed. An older API cannot show a dead apply link.
apply: z.object({ enabled: z.boolean() }).optional(),

// An absent field reads as an open season. An older API must not put the
// checker into past tense and tell families that enrollment ended.
enrollment: z.object({ enabled: z.boolean() }).optional()
```

A required field fails the full payload parse against an older API. All other features then
stop at the same time. The 2 fields above have opposite defaults on purpose.

## Hold the page until the season is known

The flag `enable_enrollment` selects the page component. The landing route replaces
`LandingPage` with `ClosedPage`. A render before the first poll shows one season, then
replaces it. A user reads that behavior as a fault.

The component `SeasonGate` holds the page content until the season resolves. `CheckerShell`
contains it. `SeasonGate` shows the content after the first failed poll. A checker that
cannot reach the endpoint then shows open-season text. It does not show an empty page.

Put each new season-dependent screen behind `SeasonGate`. The outage route is an exception.
That route has no season text.

## Test a phase

To test a phase, replace the endpoint values. Do not rebuild the app:

```js
await context.route('**/api/enrollment/features', (route) =>
  route.fulfill({
    json: {
      maintenanceBanner: { enabled: false, message: {} },
      outagePage: { enabled: false },
      apply: { enabled: true },
      enrollment: { enabled: false }
    }
  })
)
```

Change the `enrollment` and `apply` values to select a phase. See
[when to set each flag](when-to-set-flags.md) for the correct pair.

## Procedure: add a flag that closes a feature

Complete all 8 steps. A flag that stops at step 4 is not visible to the checker.

1. Add the constant to `FeatureFlags.cs`. Document the `false` behavior and the default.
2. Add the field to `EnrollmentCheckerFeaturesResponse` as a nested object with the shape
   `{ enabled: bool }`.
3. Set the field in `EnrollmentCheckController.GetFeatures` through `IFeatureManager`.
4. Add the base default to `appsettings.json`. Choose the safe value.
5. Add an optional field to the schema in `fetchCheckerFeatures.ts`. Document the result of
   an absent field.
6. Add a hook that holds the failure policy. Read the flag through that hook.
7. Add the flag to each `appsettings.{state}.example.json` file.
8. Set the flag in AWS AppConfig for each environment.

Steps 7 and 8 are easy to forget. See [change a flag](change-a-flag.md) for the reason that
step 7 matters.
