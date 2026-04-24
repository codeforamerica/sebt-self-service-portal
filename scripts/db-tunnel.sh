#!/usr/bin/env bash
# Open an SSM tunnel from localhost to the RDS SQL Server in a dev env.
# Usage: scripts/db-tunnel.sh <dc|co> [local-port]
# Leaves the tunnel in the foreground; Ctrl-C to close. See the team's
# Notion workspace for setup & troubleshooting.

set -euo pipefail

STATE="${1:?usage: db-tunnel.sh <dc|co> [local-port]}"
LOCAL_PORT="${2:-11433}"

case "$STATE" in
  dc|co) ;;
  *) echo "error: state must be 'dc' or 'co' (got: $STATE)" >&2; exit 1 ;;
esac

export AWS_PROFILE="sebt-dev-${STATE}"

if ! aws sts get-caller-identity >/dev/null 2>&1; then
  echo "SSO session expired for ${AWS_PROFILE}; logging in..."
  aws sso login
fi

BASTION_TAG="sebt-portal-${STATE}-development-bastion"
DB_IDENTIFIER="sebt-portal-${STATE}-development-db"

INSTANCE=$(aws ec2 describe-instances \
  --filters "Name=tag:Name,Values=${BASTION_TAG}" \
            "Name=instance-state-name,Values=running" \
  --query 'Reservations[0].Instances[0].InstanceId' \
  --output text)

if [[ -z "$INSTANCE" || "$INSTANCE" == "None" ]]; then
  echo "error: no running bastion found with Name tag '${BASTION_TAG}' in ${AWS_PROFILE}" >&2
  exit 1
fi

read -r RDS_ENDPOINT SECRET_ARN <<< "$(aws rds describe-db-instances \
  --db-instance-identifier "$DB_IDENTIFIER" \
  --query 'DBInstances[0].[Endpoint.Address,MasterUserSecret.SecretArn]' \
  --output text)"

# Fetch the username from the secret (non-sensitive, helpful in the banner so
# users can copy/paste the connect command without looking anything up). The
# password itself is never fetched or printed by this script.
USERNAME=$(aws secretsmanager get-secret-value \
  --secret-id "$SECRET_ARN" \
  --query SecretString --output text | jq -r .username)

cat <<EOF
════════════════════════════════════════════════════════════════════════
Tunnel:     localhost:${LOCAL_PORT}  ->  ${RDS_ENDPOINT}:1433
Bastion:    ${INSTANCE}
Profile:    ${AWS_PROFILE}
Username:   ${USERNAME}
Stop:       Ctrl-C in this terminal
════════════════════════════════════════════════════════════════════════

Pick ONE of the two options below (CLI or GUI) and paste into a new terminal.


OPTION A — CLI (runs sqlcmd directly against the tunnel)

########## COPY EVERYTHING BELOW ##########
SQLCMDPASSWORD=\$(aws secretsmanager get-secret-value \\
  --secret-id '${SECRET_ARN}' \\
  --profile ${AWS_PROFILE} \\
  --query SecretString --output text | jq -r .password) \\
  sqlcmd -S localhost,${LOCAL_PORT} -U ${USERNAME} -C
###########################################


OPTION B — GUI (fetches password to clipboard for VS Code / DBeaver / Rider)

########## COPY EVERYTHING BELOW ##########
aws secretsmanager get-secret-value \\
  --secret-id '${SECRET_ARN}' \\
  --profile ${AWS_PROFILE} \\
  --query SecretString --output text | jq -r .password | pbcopy
###########################################

Then open your SQL client (VS Code + MSSQL extension, DBeaver, or Rider's
Database tool window) and connect to:
  Server:    localhost,${LOCAL_PORT}
  Username:  ${USERNAME}
  Password:  paste from clipboard (Cmd-V)
  Option:    enable "Trust Server Certificate"

EOF

# Replace shell with aws ssm so Ctrl-C delivers SIGINT directly and the
# session-manager-plugin child is torn down cleanly.
exec aws ssm start-session \
  --target "$INSTANCE" \
  --document-name AWS-StartPortForwardingSessionToRemoteHost \
  --parameters "host=${RDS_ENDPOINT},portNumber=1433,localPortNumber=${LOCAL_PORT}"
