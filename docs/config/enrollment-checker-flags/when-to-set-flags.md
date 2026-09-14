# When to Set Each Flag

This page gives the correct flag value for each phase of a program year.

## Why enrollment and apply are 2 flags

The 2 flags look the same. A closed application window removes the apply link. A closed
season also removes the apply link. The difference appears at the start of a program year.

A state can enroll students automatically before it opens the online application. The value
of `enable_apply` is `false` in that phase and in the phase after the season. The checker
must give a different message in each one:

| Phase | Application window | Enrollment | Correct message |
| --- | --- | --- | --- |
| Start of the season | closed | active | Applications open soon. Sign up for a notice. |
| After the season | closed | complete | Enrollment for 2026 is now closed. |

One flag cannot separate these 2 phases. A checker that uses only `enable_apply` tells
families that enrollment is closed while the state still enrolls students. That message is
the worst possible error. It tells eligible families to stop.

The 2 flags have different scopes. `enable_enrollment` shows if the program is active.
`enable_apply` shows if the application window inside an active season is open.

The flag `enable_apply` has no effect on the checker when `enable_enrollment` is `false`. A
closed season always removes the apply links. The portal reads the same flag. Set
`enable_apply` for both surfaces together.

## The phase matrix

| Phase | `enable_enrollment` | `enable_apply` | `enable_checker_income_eligibility` | Checker behavior |
| --- | --- | --- | --- | --- |
| 1. Before the season. Enrollment is active. The application window is not open. | `true` | `false` | `true` | Present tense. No apply link. |
| 2. Open season. Enrollment is active. The application window is open. | `true` | `true` | `true` | Present tense. Apply link. Income eligibility tool. |
| 3. Late season. Enrollment is active. The application window is closed. | `true` | `false` | `true` | Present tense. No apply link. Income eligibility tool. |
| 4. After the season. | `false` | no effect | no effect | Past tense. The check operates. No apply links. |

An apply link needs 2 conditions. The flag `enable_apply` must be `true`. The build must
also set `NEXT_PUBLIC_APPLICATION_URL`. One condition alone hides the link. A state with no
destination in its bundle cannot open applications from AWS AppConfig alone.

## What phase 4 changes

| Screen | Open season | Closed season |
| --- | --- | --- |
| Landing page at `/` | "Get a one-time payment of $120". | "Enrollment for 2026 is now closed". |
| Disclaimer | "if you need to apply". | "whether your student was enrolled". |
| Check form | "Check if your student needs to apply". | "Check if your student was enrolled". |
| Result, enrolled | Success alert. Portal button. | Past tense text. Portal link. Portal button. |
| Result, not enrolled | Explanation. Eligibility accordion. Income eligibility tool. Apply block. | Title and the next-check card only. |
| Result, error | Present tense portal text. | Past tense portal text. Next-check card. |

The check itself operates in both seasons. A family can still ask about a student. Only the
text and the apply paths change.

The route `/closed` always shows the closed landing page. It ignores the flag. A content
reviewer can read that page while the season is open.

## The income eligibility figures

The flag `enable_checker_income_eligibility` also controls the figures for the tool. The API
sends `EnrollmentChecker:IncomeEligibility` as `null` when the flag is `false`. The checker
then removes the tool. It does not use old figures.

The figures follow 185% of the federal poverty guideline. The federal government issues new
figures each year. Review the figures each year before you set this flag to `true`. Read
AWS AppConfig for the current values.
