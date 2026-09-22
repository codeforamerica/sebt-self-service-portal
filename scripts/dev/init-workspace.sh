#!/usr/bin/env bash
#
# Prepare a workspace for local development on the SEBT portal.
#
# The script builds the two-repository layout the portal expects, verifies the
# toolchain the repository pins, installs the dependencies, and builds the
# solution. It stops at the point where the app is ready to start.
#
# It checks the toolchain, and it does not install it. A developer machine holds
# one Node and one .NET for every repository on it, so a setup script is the
# wrong place to change either. Each check therefore reports the version it
# wants and the command that installs it, and the developer runs that command.
#
# No version is written here. Each one comes from a config file in the portal
# checkout, so a bump to that file is the only edit a bump needs:
#
#   .nvmrc        Node
#   package.json  pnpm, from engines.pnpm
#   global.json   the .NET SDK
#
# Thus the script clones the portal before it checks a version. Git is the one
# prerequisite it cannot report on, because it needs git to reach the file that
# holds the others.
#
# The Aspire CLI is deliberately out of scope. It is a global tool with its own
# one-time steps, including a certificate trust prompt that needs a person.
# Read "Local development with Aspire" in README.md.
#
# The Windows twin of this script is init-workspace.ps1. Keep the two in step.
#
# Usage:
#   ./init-workspace.sh [--workspace DIR] [--ssh] [--system-certs]
#                       [--ca-bundle FILE]

set -euo pipefail

PORTAL_DIR_NAME="sebt-self-service-portal"
DC_DIR_NAME="sebt-self-service-portal-dc-connector"

PORTAL_REPO_HTTPS="https://github.com/codeforamerica/${PORTAL_DIR_NAME}.git"
DC_REPO_HTTPS="https://github.com/codeforamerica/${DC_DIR_NAME}.git"
PORTAL_REPO_SSH="git@github.com:codeforamerica/${PORTAL_DIR_NAME}.git"
DC_REPO_SSH="git@github.com:codeforamerica/${DC_DIR_NAME}.git"

WORKSPACE=""
USE_SSH=0
SYSTEM_CERTS=0
CA_BUNDLE=""

DC_CONNECTOR_PRESENT=0

usage() {
    cat <<'EOF'
Prepare a workspace for local development on the SEBT portal.

Usage: ./init-workspace.sh [options]

The script checks the toolchain against the versions this repository pins, and
it does not install it. A failed check prints the command that fixes it.

Options:
  -w, --workspace DIR   Parent directory that holds both repositories.
                        Defaults to the parent of this checkout when the script
                        runs from inside one, and to ./sebt-portal-workspace
                        otherwise.
      --ssh             Clone over SSH instead of HTTPS.
      --system-certs    Trust the operating system certificate store for Node
                        and pnpm. Use this behind a firewall that inspects TLS.
      --ca-bundle FILE  Also trust an explicit PEM bundle. Implies
                        --system-certs. Use this when the proxy root
                        certificate is a file rather than a keychain entry.
  -h, --help            Show this message.

The script stops when a required tool is missing or when no container runtime is
available. It continues without the DC connector, and it says so in the summary
at the end.
EOF
}

step() { printf '\n==> %s\n' "$1"; }
info() { printf '    %s\n' "$1"; }
# Reports a problem the script is not going to stop for. Every one of these has
# a matching paragraph in the summary, so the message here stays short.
notice() { printf '    WARNING: %s\n' "$1"; }
fail() {
    printf '\nERROR: %s\n\n' "$1" >&2
    exit 1
}

while [ $# -gt 0 ]; do
    case "$1" in
        -w|--workspace)
            [ $# -ge 2 ] || fail "--workspace needs a directory."
            WORKSPACE="$2"
            shift 2
            ;;
        --ssh)
            USE_SSH=1
            shift
            ;;
        --system-certs)
            SYSTEM_CERTS=1
            shift
            ;;
        --ca-bundle)
            [ $# -ge 2 ] || fail "--ca-bundle needs a file."
            CA_BUNDLE="$2"
            SYSTEM_CERTS=1
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            fail "Unknown option '$1'. Run with --help."
            ;;
    esac
done

# Reads one dotted path out of a JSON file. Node is the parser, so this runs
# only after check_node. The alternative, a grep for a quoted key, reads the
# wrong "version" as soon as a file grows a second one.
json_value() {
    node -e '
      const [file, path] = process.argv.slice(1);
      const doc = JSON.parse(require("fs").readFileSync(file, "utf8"));
      const value = path.split(".").reduce((node, key) => node && node[key], doc);
      if (value == null) { process.exit(1); }
      process.stdout.write(String(value));
    ' "$1" "$2"
}

# The leading integer of a version string. `v25.6.1` and `>=10.0.0` both give
# their major, which is the only part any of these pins constrains.
major_of() {
    printf '%s' "$1" | tr -cd '0-9.\n' | cut -d. -f1
}

apply_cert_policy() {
    [ "$SYSTEM_CERTS" -eq 1 ] || return 0

    step "Trusting the system certificate store"

    # Node reads its own bundle by default, so a proxy root in the keychain is
    # invisible to it and to pnpm. This is the supported opt in, and it keeps
    # verification on. Never reach for strict-ssl=false here.
    export NODE_OPTIONS="${NODE_OPTIONS:-} --use-system-ca"
    info "Node and pnpm now read the operating system trust store."

    if [ -n "$CA_BUNDLE" ]; then
        [ -f "$CA_BUNDLE" ] || fail "CA bundle '$CA_BUNDLE' not found."
        # One file, four consumers: Node adds it to its own bundle, OpenSSL
        # covers .NET on Linux, and curl and git each read their own variable.
        export NODE_EXTRA_CA_CERTS="$CA_BUNDLE"
        export SSL_CERT_FILE="$CA_BUNDLE"
        export CURL_CA_BUNDLE="$CA_BUNDLE"
        export GIT_SSL_CAINFO="$CA_BUNDLE"
        info "Added $CA_BUNDLE for Node, .NET, curl, and git."
    fi
}

resolve_workspace() {
    if [ -n "$WORKSPACE" ]; then
        return 0
    fi

    # Running from inside a checkout is the common case for an existing
    # developer. The workspace is then the directory that already holds it.
    local script_dir checkout
    script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)

    if checkout=$(git -C "$script_dir" rev-parse --show-toplevel 2>/dev/null); then
        WORKSPACE=$(dirname "$checkout")
    else
        WORKSPACE="$PWD/sebt-portal-workspace"
    fi
}

ensure_git() {
    step "Checking git"
    command -v git >/dev/null 2>&1 ||
        fail "git is not installed. Install it from https://git-scm.com/downloads and run this script again."
    info "$(git --version)"
}

clone_repo() {
    local url="$1" dir="$2" label="$3"

    if [ -d "$dir/.git" ]; then
        info "$label is already cloned at $dir."
        return 0
    fi

    git clone "$url" "$dir"
}

check_node() {
    local required required_major installed installed_major
    required=$(tr -d ' \t\r\nv' < "$PORTAL/.nvmrc")
    required_major=$(major_of "$required")

    step "Checking Node (.nvmrc pins $required)"

    installed=$(node --version 2>/dev/null || true)
    installed_major=$(major_of "$installed")

    if [ -n "$installed_major" ] && [ "$installed_major" -ge "$required_major" ] 2>/dev/null; then
        info "Node $installed satisfies the pin."
        return 0
    fi

    if [ -z "$installed" ]; then
        fail "Node is not installed, and this repository needs version ${required_major} or later.
Install it, then run this script again:
  macOS         brew install node
  Windows       winget install OpenJS.NodeJS
  any platform  https://nodejs.org/en/download"
    fi

    fail "Node $installed is installed, and this repository needs version ${required_major} or later.
Upgrade it, then run this script again:
  macOS         brew upgrade node
  Windows       winget upgrade OpenJS.NodeJS
  any platform  https://nodejs.org/en/download"
}

check_pnpm() {
    local required required_major installed installed_major
    required=$(json_value "$PORTAL/package.json" engines.pnpm) ||
        fail "package.json has no engines.pnpm entry, so the pnpm version cannot be read."
    required_major=$(major_of "$required")

    step "Checking pnpm (package.json pins $required)"

    installed=$(pnpm --version 2>/dev/null || true)
    installed_major=$(major_of "$installed")

    if [ -n "$installed_major" ] && [ "$installed_major" -ge "$required_major" ] 2>/dev/null; then
        info "pnpm $installed satisfies the pin."
        return 0
    fi

    if [ -z "$installed" ]; then
        fail "pnpm is not installed, and this repository needs version ${required_major} or later.
Install it, then run this script again:
  corepack enable pnpm     (corepack ships with Node, so this needs no download)
  or read https://pnpm.io/installation"
    fi

    fail "pnpm $installed is installed, and this repository needs version ${required_major} or later.
Upgrade it, then run this script again:
  corepack prepare pnpm@latest --activate
  or read https://pnpm.io/installation"
}

check_dotnet() {
    local pinned
    pinned=$(json_value "$PORTAL/global.json" sdk.version) ||
        fail "global.json has no sdk.version entry, so the .NET SDK version cannot be read."

    step "Checking the .NET SDK (global.json pins $pinned)"

    command -v dotnet >/dev/null 2>&1 ||
        fail "The .NET SDK is not installed, and global.json asks for $pinned.
Install it, then run this script again:
  macOS         brew install dotnet
  Windows       winget install Microsoft.DotNet.SDK.10
  any platform  https://dotnet.microsoft.com/download"

    # `dotnet --version` inside the checkout resolves global.json, including its
    # rollForward. A zero exit is therefore the whole check: it says an SDK is
    # installed and that this repository accepts it.
    if (cd "$PORTAL" && dotnet --version >/dev/null 2>&1); then
        info ".NET SDK $(cd "$PORTAL" && dotnet --version) satisfies global.json."
        return 0
    fi

    fail "No installed .NET SDK satisfies global.json, which asks for $pinned.
Installed SDKs:
$(dotnet --list-sdks 2>/dev/null | sed 's/^/  /')
Install $pinned or a later patch of it from https://dotnet.microsoft.com/download, then run this script again."
}

check_container_runtime() {
    step "Checking for a container runtime"

    local runtime installed=""
    for runtime in docker podman nerdctl; do
        command -v "$runtime" >/dev/null 2>&1 || continue
        installed="$installed $runtime"

        if "$runtime" info >/dev/null 2>&1; then
            info "$runtime is installed and responding."
            return 0
        fi
    done

    # An installed but stopped runtime is a different problem from an absent
    # one, and it has a different fix, so the two get different messages.
    if [ -n "$installed" ]; then
        fail "A container runtime is installed ($(printf '%s' "$installed" | sed 's/^ //')) but it is not responding.
Start it and run this script again. The local stack needs it for the databases, Redis, Keycloak, and Mailpit."
    fi

    fail "No container runtime is installed, and the local stack cannot start the databases, Redis, Keycloak, or Mailpit without one.
Install one and run this script again:
  Podman        https://podman.io/docs/installation   (then: podman machine init && podman machine start)
  Docker        https://www.docker.com/products/docker-desktop
Podman is the usual choice where Docker Desktop licensing is a problem."
}

summary() {
    printf '\n'
    printf '%s\n' "----------------------------------------------------------------------"
    printf 'Workspace ready: %s\n' "$WORKSPACE"
    printf '%s\n' "----------------------------------------------------------------------"
    printf '\n'
    printf 'Toolchain\n'
    printf '  Node        %s\n' "$(node --version)"
    printf '  pnpm        %s\n' "$(pnpm --version)"
    printf '  .NET SDK    %s\n' "$(cd "$PORTAL" && dotnet --version)"
    printf '\n'

    if [ "$DC_CONNECTOR_PRESENT" -eq 1 ]; then
        printf 'States      DC and CO are both ready.\n'
    else
        printf 'States      CO only. You cannot run the DC configuration.\n'
        printf '            The DC connector is not at\n'
        printf '            %s.\n' "$DC_CONNECTOR"
        printf '            Clone it beside the portal, or point DC_CONNECTOR_PATH at your\n'
        printf '            checkout, then run this script again.\n'
    fi
    printf '\n'

    printf 'Next\n'
    printf '  cd %s\n' "$PORTAL"
    printf '\n'
    printf '  Running the app needs one more choice, and README.md covers both:\n'
    printf '    "Start services" and "Build and run the app" for the Compose path.\n'
    printf '    "Local development with Aspire" for the Aspire path, which has its own\n'
    printf '    one-time steps, including a certificate trust prompt that needs a person.\n'
    printf '\n'
}

apply_cert_policy
ensure_git
resolve_workspace

PORTAL="$WORKSPACE/$PORTAL_DIR_NAME"
DC_CONNECTOR="$WORKSPACE/$DC_DIR_NAME"

step "Preparing the workspace at $WORKSPACE"
mkdir -p "$WORKSPACE"

step "Cloning the portal"
if [ "$USE_SSH" -eq 1 ]; then
    portal_url="$PORTAL_REPO_SSH"
else
    portal_url="$PORTAL_REPO_HTTPS"
fi
clone_repo "$portal_url" "$PORTAL" "The portal" ||
    fail "Could not clone the portal from $portal_url.
Behind a TLS-inspecting proxy, re-run with --system-certs. For a private fork, re-run with --ssh."

# Every version below comes out of the checkout above, so nothing here runs
# before the clone.
check_node
check_pnpm
check_dotnet
check_container_runtime

step "Cloning the DC connector"
# DC is the one state whose connector lives outside this repository, and a
# developer without access to it still has a working CO workspace. So a failure
# here is a warning and a line in the summary, and not the end of the run.
if [ "$USE_SSH" -eq 1 ]; then
    dc_url="$DC_REPO_SSH"
else
    dc_url="$DC_REPO_HTTPS"
fi
if clone_repo "$dc_url" "$DC_CONNECTOR" "The DC connector"; then
    DC_CONNECTOR_PRESENT=1
else
    # The summary spells out what this costs, so it does not queue a warning too.
    notice "The DC connector could not be cloned. CO will work and DC will not. Read the summary."
fi

step "Installing JavaScript dependencies"
(cd "$PORTAL" && pnpm install)

step "Building the .NET solution"
(cd "$PORTAL" && dotnet build SEBT.slnx)

summary
