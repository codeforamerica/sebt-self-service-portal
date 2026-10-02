---
description: Settings for the standalone enrollment checker, including income screening and the maintenance banner.
keywords: enrollment checker income eligibility threshold maintenance banner match exact
---

# Enrollment checker

The enrollment checker is a separate app that answers one question for an unauthenticated visitor: is this child
enrolled? It has its own flags and its own settings block.

Because it is often hosted as static files with no server of its own, it polls the portal's enrollment features
endpoint at runtime. Flags can therefore be changed through AppConfig without rebuilding or redeploying it.

## Flags

| Flag | Default | Controls |
| --- | --- | --- |
| `enable_checker_income_eligibility` | unset | Income screening on the not-enrolled result |
| `enrollment_check_requires_at_least_one_exact_matched_field` | unset | Drops weak candidate matches |
| `enable_checker_maintenance_banner` | `false` | The maintenance banner |
| `checker_outage_page_enabled` | `false` | The outage page |

The last two are covered in [Outages and maintenance](outages.md).

## Income screening

When a check comes back not enrolled, the checker can offer a rough income screen instead of ending there.
Thresholds come from `EnrollmentChecker:IncomeEligibility`:

| Field | Meaning |
| --- | --- |
| `BaseThreshold` | Income limit for the smallest household |
| `PerMemberIncrement` | Added to the limit for each additional member |
| `MaxHouseholdSize` | The largest household the table covers |

These figures change yearly. The flag exists so a state can withdraw the tool between updates rather than screen
people against stale numbers, which is the failure mode worth avoiding: a wrong answer here tells a family they are
ineligible when they are not.

## Match strictness

`enrollment_check_requires_at_least_one_exact_matched_field` drops candidates, whether Match or PossibleMatch, where
neither the date of birth nor the full first and last name matches what was submitted exactly.

It trades recall for precision. On, a family whose record holds a nickname or a transposed birth date may be told
they are not enrolled. Off, a loose match may show one family another family's enrollment status. The flag is
documented as defaulting on for Colorado.

## Maintenance banner copy

`EnrollmentChecker:MaintenanceBanner:Message` is a map of language code to message. The checker picks the entry
matching the visitor's active language on the client side.

Copy lives here rather than in the content pipeline so it can be changed during an incident without a content
regeneration and redeploy. A language with no entry shows no banner, so add every supported language when setting
one.
