---
description: The two flags that control whether a season is enrolling and whether applications are open.
keywords: season enrollment applications apply enable_enrollment enable_apply open closed
---

# Season and applications

Two flags describe where a state is in its program year. They are separate because a state can stop taking new
applications while the season is still running.

| Flag | Default | Controls |
| --- | --- | --- |
| `enable_enrollment` | `true` | Whether the season is still enrolling |
| `enable_apply` | unset | Whether applications are open |

## enable_enrollment

The wider of the two. Turning it off switches the enrollment checker to past-tense copy, asking whether a student
*was* enrolled rather than whether they are, and drops every path that would lead someone to apply.

It is set to `true` in the base `appsettings.json` on purpose. If it were unset by default, a state that never
declared it would have its season read as closed.

## enable_apply

Covers only the application window inside a season. A state can pause applications, for example while it works
through a backlog, and leave `enable_enrollment` on.

Unset reads as closed, so applications stay shut unless a state opens them deliberately. Turning it on is not
sufficient on its own: both apps also need an apply destination configured, and with no destination the apply
interface stays hidden regardless of the flag.

## How they combine

| `enable_enrollment` | `enable_apply` | Result |
| --- | --- | --- |
| `true` | `true` | Season open, applications accepted |
| `true` | `false` or unset | Season open, applications paused |
| `false` | any | Season closed; the checker uses past-tense copy and no apply path is shown |

Because the second flag is ignored once the first is off, closing a season takes one change rather than two.
