---
description: Taking the portal or the enrollment checker down, either on a planned schedule or immediately.
keywords: outage maintenance banner downtime schedule window outage_page_enabled checker_outage_page_enabled
---

# Outages and maintenance

Each app can be taken down independently, either by a planned window or by an immediate switch.

| Flag | Default | Controls |
| --- | --- | --- |
| `outage_page_enabled` | `false` | Sitewide outage page on the portal |
| `checker_outage_page_enabled` | `false` | Sitewide outage page on the enrollment checker |
| `enable_checker_maintenance_banner` | `false` | Maintenance banner on the checker, which stays usable |

A banner warns; an outage page replaces the site.

## Planned windows

The `OutageSchedule` section holds windows:

| Field | Meaning |
| --- | --- |
| `TimeZoneId` | The zone the window times are read in. Defaults to `America/Denver`. |
| `Windows[].Start`, `Windows[].End` | When the window opens and closes |
| `Windows[].Target` | Which surface it applies to, the portal or the enrollment checker |

`TimeZoneId` is worth setting deliberately. The default is Mountain time, so a state in another zone that leaves it
alone will find its windows opening at the wrong hour.

## Which wins, the schedule or the flag

Resolved per surface, not globally:

- Where a window targets a surface, **the schedule is the authority** and that surface's flag is ignored.
- Where no window targets a surface, **the flag is the authority**.

This is what keeps the flags useful for unscheduled emergencies. A state with a maintenance window configured for the
checker can still take the portal down immediately with `outage_page_enabled`, because the two surfaces resolve
separately.

One consequence: when a window is in force, toggling that surface's flag does nothing. If a flag appears not to
work, check the schedule before anything else.

## Turning something off right now

`outage_page_enabled` and `checker_outage_page_enabled` are the fastest lever, because AppConfig applies without a
redeploy and the checker polls for changes rather than needing a rebuild.

Banner copy is per language, from the `EnrollmentChecker:MaintenanceBanner:Message` map. See
[Enrollment checker](enrollment-checker.md).
