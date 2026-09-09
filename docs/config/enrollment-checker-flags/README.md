# Enrollment Checker Feature Flags

The Enrollment Checker tells a family if a student is enrolled in Summer EBT. A state
program moves through phases during the year. Three feature flags close checker features as
the program moves.

The constants are in
[`FeatureFlags.cs`](../../../apps/portal/src/SEBT.Portal.Core/AppSettings/FeatureFlags.cs).
Always use the constant. Do not write a flag name as a text literal in the code.

| Flag | Value `true` | Value `false` | Value not set |
| --- | --- | --- | --- |
| `enable_enrollment` | The checker uses present tense. It asks if a student will be enrolled. | The checker uses past tense. It asks if a student was enrolled. It also removes all apply links. | `false` |
| `enable_apply` | The checker can show apply links. A destination is also necessary. | The checker hides all apply links and apply buttons. | `false` |
| `enable_checker_income_eligibility` | The checker shows the income eligibility tool on the not-enrolled result. | The checker removes the income eligibility tool. | `false` |

**The default value of `enable_enrollment` is unsafe.** A flag that nobody sets reads as
`false`. That value puts a state into past tense. The checker then tells families that
enrollment ended.

For this reason the base `appsettings.json` sets `enable_enrollment` to `true`. An omission
then gives an open season. To close a season, set the flag to `false`. Do not remove the
flag.

## Pages

| Page | Read this page to learn |
| --- | --- |
| [When to set each flag](when-to-set-flags.md) | Which value each flag needs in each program phase |
| [Change a flag](change-a-flag.md) | Where a value lives, how to edit it, and how long it takes |
| [Use flags in code](using-flags-in-code.md) | How to read a flag, and how to add a new one |

## Scope

These pages do not describe `checker_outage_page_enabled` or
`enable_checker_maintenance_banner`. Those 2 flags stop the full checker for operational
reasons. They obey different precedence rules.
