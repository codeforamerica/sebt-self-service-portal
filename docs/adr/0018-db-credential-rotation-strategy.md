# ADR 0018: DB Credential Rotation Strategy

Date: 2026-07-07

## Status

Proposed

## Context

RDS SQL Server uses password-based authentication as IAM database authentication is not supported 
for SQL Server. AWS rotates the RDS master user password every seven days via 
`manage_master_user_password = true`. ECS tasks receive `DB_USER` and `DB_PASSWORD` as environment 
variables injected from Secrets Manager at task launch time which means a running container never
refreshes these values.

When rotation fires, running ECS tasks continue using the old (now invalid) password for any new 
connection attempts once the connection pool is exhausted or pruned. Both ECS deployments 
(CO and DC) are affected: the Fargate module sets `deployment_minimum_healthy_percent = 100` and 
`deployment_maximum_percent` unset (relying on the AWS ECS default of 200), so a rolling restart 
always starts a new task before draining the old one. The old task continues serving traffic during the 30-60 second spinup window and can 
fail to open fresh connections against the rotated password.

We evaluated the following options:

**Option A — RDS Proxy:** AWS RDS Proxy pins long-lived connections and rotates credentials 
transparently, with no application changes required. However, [RDS Proxy does not support 
SQL Server 2022](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/rds-proxy.html#:~:text=Currently%2C%20RDS%20Proxy%20does%20not%20support%20RDS%20for%20SQL%20Server%20DB%20instances%20that%20run%20on%20major%20version%20SQL%20Server%202022), 
which is the version currently deployed. Using a compatible version would require significant changes 
to the infrastructure and data migration which has its own potential issues.

Rejected: blocked by SQL Server 2022 incompatibility.

**Option B — App-level secret refresh:** The application detects authentication failures, fetches 
fresh credentials from Secrets Manager via an HTTP call to an AWS Secrets Manager Agent sidecar 
(keeping the main codebase cloud-agnostic), and retries. This fully closes the error window but 
requires non-trivial C# retry and credential-refresh plumbing plus a new sidecar container. 
This approach is not feasible as it would require a new AWS-specific sidecar container that may 
not be replicable with other cloud providers.

Rejected: requires significant application changes and an AWS-specific sidecar container.

**Option C — EventBridge-triggered ECS restart (implemented, superseded):** A Lambda watches for 
`RotationSucceeded` EventBridge events from the master secret and calls 
`ECS:UpdateService(forceNewDeployment=True)`. This resolves the problem for idle connection pools 
but leaves a small error window: the old task serves traffic while the new task starts 
(30-60 seconds), and any new connections the old task opened during that window fail because the 
master password has already changed.

Rejected: leaves a small error window during ECS rolling restart.

**Option D — Alternating users rotation (chosen):** Two dedicated SQL Server logins 
(`appuser` and `appuser_clone`) alternate as the active credential each rotation cycle. A custom 
Secrets Manager rotation Lambda implements the four-step rotation protocol: it generates a new 
password for the currently inactive user (createSecret), applies it in SQL Server using admin 
credentials while the active user's credentials remain unchanged (setSecret), verifies the new 
credentials with a test connection (testSecret), then promotes the new credentials to `AWSCURRENT` 
and triggers an ECS rolling restart (finishSecret). Because the inactive user's password is updated 
before any ECS tasks are asked to use it, running tasks can open new connections throughout the 
restart. The error window is fully closed.

## Decision

Implement Option D (alternating users rotation). The rotation Lambda lives in 
`tofu/modules/sebt_database/` and is deployed as part of the database module. ECS tasks are updated 
to read `DB_USER` and `DB_PASSWORD` from a dedicated app-user Secrets Manager secret rather than the 
RDS-managed master-user secret. The master user is used only by the rotation Lambda to create and
modify the app user logins.

## Consequences

Running ECS tasks can always open new DB connections during and after rotation — the active user's 
credentials are never invalidated mid-cycle. The rotation Lambda runs inside the VPC 
(private subnets) to reach RDS on TCP 1433, requiring a Lambda security group and a new inbound rule
on the RDS security group. Initial deployment is split across two sequential changes: the first deploys the rotation Lambda 
and creates the app-user logins; the second updates ECS tasks to read credentials from the 
app-user secret. The two changes must be applied in order.
