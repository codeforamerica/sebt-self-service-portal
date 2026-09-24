#!/usr/bin/env bash
#
# Prepare a workspace for local development on the SEBT portal.
#
# The script builds the two-repository layout the portal expects, puts the
# toolchain the repository pins in place, installs the dependencies, builds the
# solution, and installs the Aspire CLI. It ends at the point where one command
# starts the app.
#
# It asks before it installs anything, and it installs into your home directory.
# Node goes to the tools directory below and the .NET SDK goes to ~/.dotnet, so
# a machine-wide Node or .NET stays as it is, and no step needs sudo. Decline
# any offer and the script prints the command that does that step by hand.
#
# No version is written here. Each one comes from a config file in the portal
# checkout, so a bump to that file is the only edit a bump needs:
#
#   .nvmrc             Node
#   package.json       pnpm, from engines.pnpm
#   global.json        the .NET SDK
#   aspire.config.json the Aspire CLI, from sdk.version
#
# Thus the script clones the portal before it reads a version. Git is the one
# prerequisite it cannot install, because it needs git to reach the file that
# holds the others.
#
# Podman is the exception to the pinned-download rule. The repository pins no
# container runtime, and Podman has no user-local tarball worth maintaining, so
# that one offer goes through the platform package manager.
#
# The Windows twin of this script is init-workspace.ps1. Keep the two in step.
#
# Usage:
#   ./init-workspace.sh [--workspace DIR] [--ssh] [--yes] [--check-only]
#                       [--system-certs] [--ca-bundle FILE]

set -euo pipefail

PORTAL_DIR_NAME="sebt-self-service-portal"
DC_DIR_NAME="sebt-self-service-portal-dc-connector"

PORTAL_REPO_HTTPS="https://github.com/codeforamerica/${PORTAL_DIR_NAME}.git"
DC_REPO_HTTPS="https://github.com/codeforamerica/${DC_DIR_NAME}.git"
PORTAL_REPO_SSH="git@github.com:codeforamerica/${PORTAL_DIR_NAME}.git"
DC_REPO_SSH="git@github.com:codeforamerica/${DC_DIR_NAME}.git"

# Everything the script installs for Node lands here. One directory keeps the
# whole footprint visible, and removing it undoes every Node-side change.
TOOLS_DIR="${SEBT_TOOLS_DIR:-${XDG_DATA_HOME:-$HOME/.local/share}/sebt}"
DOTNET_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"

WORKSPACE=""
BRANCH=""
USE_SSH=0
SYSTEM_CERTS=0
CA_BUNDLE=""
ASSUME_YES=0
CHECK_ONLY=0

DC_CONNECTOR_PRESENT=0
ASPIRE_READY=0
CERTS_TRUSTED=0
# Directories this run put on PATH. The summary offers to make them permanent.
PATH_ADDITIONS=()
# One line per thing the script installed, for the summary.
INSTALLED=()
# Temporary download directories. The trap below clears them on every exit
# path, including the error paths, which a per-function RETURN trap misses.
CLEANUP_DIRS=()

cleanup() {
    local dir
    for dir in ${CLEANUP_DIRS+"${CLEANUP_DIRS[@]}"}; do
        rm -rf "$dir"
    done
}
trap cleanup EXIT

usage() {
    cat <<'EOF'
Prepare a workspace for local development on the SEBT portal.

Usage: ./init-workspace.sh [options]

The script asks before it installs a missing tool, and it installs the version
this repository pins into your home directory. Decline an offer and it prints
the command that does that step by hand.

Options:
  -w, --workspace DIR   Parent directory that holds both repositories.
                        Defaults to the parent of this checkout when the script
                        runs from inside one, and to ./sebt-portal-workspace
                        otherwise.
      --branch NAME     Clone this branch of the portal rather than the
                        default one. Use this to set up from a branch whose
                        changes have not merged yet.
      --ssh             Clone over SSH instead of HTTPS.
  -y, --yes             Accept every install offer without asking. Use this for
                        an unattended run.
      --check-only      Decline every install offer. The script still clones,
                        installs the JavaScript dependencies, and builds the
                        solution, and it stops at the first tool it needs and
                        does not have.
      --system-certs    Behind a firewall that inspects TLS, use this one flag.
                        It points git, Node, pnpm, curl, and .NET at the trust
                        store your machine already has.
      --ca-bundle FILE  Use an explicit PEM bundle rather than the system trust
                        store. Implies --system-certs. Reach for this only when
                        the proxy root is a file that was never added to the
                        store.
  -h, --help            Show this message.

Environment:
  SEBT_TOOLS_DIR        Where to put a downloaded Node. Defaults to
                        ~/.local/share/sebt.
  DOTNET_INSTALL_DIR    Where to put a downloaded .NET SDK. Defaults to
                        ~/.dotnet.

The script stops when a required tool is missing and you decline to install it.
It continues without the DC connector, and it says so in the summary at the end.
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
        --branch)
            [ $# -ge 2 ] || fail "--branch needs a name."
            BRANCH="$2"
            shift 2
            ;;
        --ssh)
            USE_SSH=1
            shift
            ;;
        -y|--yes)
            ASSUME_YES=1
            shift
            ;;
        --check-only)
            CHECK_ONLY=1
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

[ "$ASSUME_YES" -eq 1 ] && [ "$CHECK_ONLY" -eq 1 ] &&
    fail "--yes and --check-only ask for opposite things. Pass one of them."

# Asks a yes or no question and answers it for the caller under --yes and
# --check-only. The read is from the terminal, not from stdin, because
# `curl ... | bash` leaves stdin holding the script itself.
confirm() {
    local question="$1" reply

    if [ "$CHECK_ONLY" -eq 1 ]; then
        info "Skipping (--check-only): $question"
        return 1
    fi

    if [ "$ASSUME_YES" -eq 1 ]; then
        info "Yes (--yes): $question"
        return 0
    fi

    if [ ! -t 0 ] && [ ! -r /dev/tty ]; then
        info "No terminal to ask on, so treating this as no: $question"
        return 1
    fi

    printf '    %s [y/N] ' "$question" > /dev/tty
    read -r reply < /dev/tty || reply=""
    case "$reply" in
        [yY]|[yY][eE][sS]) return 0 ;;
        *) return 1 ;;
    esac
}

# Puts a directory at the front of PATH for this run and records it, so the
# summary can offer to make it permanent. A directory that is already on PATH
# is not recorded, because there is nothing there for a profile to fix.
add_to_path() {
    local dir="$1"
    case ":$PATH:" in
        *":$dir:"*) return 0 ;;
    esac
    PATH="$dir:$PATH"
    export PATH
    PATH_ADDITIONS+=("$dir")
}

# Reads one dotted path out of a JSON file. Node is the parser, so this runs
# only after Node is in place. The alternative, a grep for a quoted key, reads
# the wrong "version" as soon as a file grows a second one.
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

# Writes the operating system trust store to a PEM file and prints its path.
# git, curl, and .NET each read a bundle rather than the store, so a proxy root
# that lives only in the store is invisible to all 3. Node is the exception,
# because --use-system-ca reads the store directly.
system_root_bundle() {
    local out candidate

    case "$(uname -s)" in
        Darwin)
            out=$(mktemp -d)
            CLEANUP_DIRS+=("$out")
            out="$out/system-roots.pem"

            # The first keychain holds the Apple roots. The second holds what an
            # administrator added, which is where a proxy root lands, so the
            # bundle needs both or it trusts the proxy and nothing else.
            security find-certificate -a -p \
                /System/Library/Keychains/SystemRootCertificates.keychain \
                > "$out" 2>/dev/null || return 1
            security find-certificate -a -p /Library/Keychains/System.keychain \
                >> "$out" 2>/dev/null || true

            grep -q "BEGIN CERTIFICATE" "$out" || return 1
            printf '%s' "$out"
            ;;
        Linux)
            # The platform already keeps one, and a proxy root added the
            # supported way is in it.
            for candidate in /etc/ssl/certs/ca-certificates.crt \
                             /etc/pki/tls/certs/ca-bundle.crt \
                             /etc/ssl/ca-bundle.pem; do
                [ -f "$candidate" ] || continue
                printf '%s' "$candidate"
                return 0
            done
            return 1
            ;;
        *) return 1 ;;
    esac
}

# One flag covers the whole situation behind a TLS-inspecting proxy. Work out
# what that means for each tool here, rather than asking a developer to know
# which tool reads which store.
apply_cert_policy() {
    [ "$SYSTEM_CERTS" -eq 1 ] || return 0

    step "Trusting the system certificate store"

    # This is the supported opt in for Node, and it keeps verification on.
    # Never reach for strict-ssl=false here.
    export NODE_OPTIONS="${NODE_OPTIONS:-} --use-system-ca"
    info "Node and pnpm now read the operating system trust store."

    if [ -n "$CA_BUNDLE" ]; then
        [ -f "$CA_BUNDLE" ] || fail "CA bundle '$CA_BUNDLE' not found."
    elif CA_BUNDLE=$(system_root_bundle); then
        info "Read the operating system roots into a bundle for this run."
    else
        CA_BUNDLE=""
        notice "Could not read the operating system trust store, so git, curl, and .NET keep their own bundles. If a clone fails on a certificate, pass --ca-bundle <file>."
    fi

    if [ -n "$CA_BUNDLE" ]; then
        # One file, four consumers: Node adds it to its own bundle, OpenSSL
        # covers .NET on Linux, and curl and git each read their own variable.
        export NODE_EXTRA_CA_CERTS="$CA_BUNDLE"
        export SSL_CERT_FILE="$CA_BUNDLE"
        export CURL_CA_BUNDLE="$CA_BUNDLE"
        export GIT_SSL_CAINFO="$CA_BUNDLE"
        info "git, curl, and .NET now read $CA_BUNDLE."
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
        fail "git is not installed, and the script needs it to reach every other version this repository pins.
Install it and run this script again:
  macOS         xcode-select --install
  Linux         your platform package manager, such as: sudo apt install git
  any platform  https://git-scm.com/downloads"
    info "$(git --version)"
}

# Holds what git said about the last clone, so a failure can quote it rather
# than guess at it. A guess sent a developer after the wrong problem once.
CLONE_OUTPUT=""

run_clone() {
    local url="$1" dir="$2" log status

    log=$(mktemp -d)
    CLEANUP_DIRS+=("$log")
    log="$log/clone.log"

    # tee keeps the progress on screen and a copy for the message below.
    # PIPESTATUS is git's own exit code, not tee's.
    if [ -n "$BRANCH" ]; then
        git clone --branch "$BRANCH" "$url" "$dir" 2>&1 | tee "$log"
    else
        git clone "$url" "$dir" 2>&1 | tee "$log"
    fi
    status=${PIPESTATUS[0]}

    CLONE_OUTPUT=$(cat "$log")
    return "$status"
}

# True when git is complaining about a certificate rather than about the
# network or the repository. The wording differs by TLS backend, so this
# matches several: OpenSSL reports through libcurl in words of its own, and
# Windows schannel names itself.
is_certificate_complaint() {
    case "$1" in
        *"SSL certificate problem"*|\
        *"unable to get local issuer certificate"*|\
        *"self-signed certificate"*|\
        *"self signed certificate"*|\
        *"SSL peer certificate"*|\
        *"certificate verify failed"*|\
        *"schannel"*)
            return 0
            ;;
    esac
    return 1
}

# An existing checkout is not cloned again, so --branch would otherwise be
# ignored without a word, and the run would set itself up against whatever
# branch happened to be there. That is how a developer ends up on main
# wondering where the AppHost went.
ensure_branch() {
    local dir="$1" label="$2" current

    [ -n "$BRANCH" ] || return 0

    current=$(git -C "$dir" rev-parse --abbrev-ref HEAD 2>/dev/null || true)
    if [ "$current" = "$BRANCH" ]; then
        info "$label is on $BRANCH."
        return 0
    fi

    notice "$label is on ${current:-an unknown branch}, and --branch asked for $BRANCH."
    if confirm "Switch $label to $BRANCH?"; then
        # The refspec is explicit because a clone made with --depth or
        # --single-branch tracks one branch only, and a bare `fetch origin
        # <branch>` then leaves nothing for checkout to resolve.
        git -C "$dir" fetch origin "$BRANCH:refs/remotes/origin/$BRANCH" ||
            fail "Could not fetch $BRANCH into $dir.
Fetch it by hand and run this script again."

        # The first form moves to a local branch that already exists. The
        # second creates one that follows the remote.
        git -C "$dir" checkout "$BRANCH" 2>/dev/null ||
            git -C "$dir" checkout -b "$BRANCH" --track "origin/$BRANCH" ||
            fail "Fetched $BRANCH, and could not check it out in $dir.
Check for local changes in the way, then run this script again."

        info "$label is now on $BRANCH."
        return 0
    fi

    notice "Staying on ${current:-the current branch}. Every version this script reads comes from there."
}

clone_repo() {
    local url="$1" dir="$2" label="$3"

    if [ -d "$dir/.git" ]; then
        info "$label is already cloned at $dir."
        ensure_branch "$dir" "$label"
        return 0
    fi

    if run_clone "$url" "$dir"; then
        return 0
    fi

    # A certificate complaint almost always means a proxy is inspecting TLS,
    # and the fix is the one --system-certs applies. Applying it here too means
    # the common corporate machine needs no flag and no second run. Anything
    # else, such as a name that does not resolve, is not ours to retry.
    if [ "$SYSTEM_CERTS" -eq 0 ] && is_certificate_complaint "$CLONE_OUTPUT"; then
        notice "That reads like a proxy inspecting TLS. Trying again with the system trust store."
        SYSTEM_CERTS=1
        apply_cert_policy

        # git removes a directory it created when the clone fails, and an empty
        # one left by anything else would stop the retry before it starts.
        rmdir "$dir" 2>/dev/null || true

        if run_clone "$url" "$dir"; then
            info "That worked. The rest of this run uses the system trust store too."
            return 0
        fi
    fi

    return 1
}

# --- Node ------------------------------------------------------------------

node_platform() {
    local os arch
    case "$(uname -s)" in
        Darwin) os="darwin" ;;
        Linux)  os="linux" ;;
        *) return 1 ;;
    esac
    case "$(uname -m)" in
        arm64|aarch64) arch="arm64" ;;
        x86_64|amd64)  arch="x64" ;;
        *) return 1 ;;
    esac
    printf '%s-%s' "$os" "$arch"
}

# Downloads the newest release of the pinned Node major and verifies it against
# the checksum file that release publishes. The .tar.gz is on purpose: the .xz
# is smaller, but unpacking it needs an xz that a bare machine may not have.
install_node() {
    local required_major="$1"
    local platform dist_url shasums entry filename version checksum tmp

    platform=$(node_platform) ||
        fail "This script has no Node download for $(uname -s) $(uname -m).
Install Node ${required_major} from https://nodejs.org/en/download and run this script again."

    dist_url="https://nodejs.org/dist/latest-v${required_major}.x"

    info "Resolving the newest Node ${required_major} release..."
    shasums=$(curl -fsSL "$dist_url/SHASUMS256.txt") ||
        fail "Could not reach $dist_url/SHASUMS256.txt.
Check your network, or install Node ${required_major} from https://nodejs.org/en/download and run this script again."

    entry=$(printf '%s\n' "$shasums" | grep -E "  node-v${required_major}\.[0-9.]+-${platform}\.tar\.gz$" | head -1) ||
        entry=""
    [ -n "$entry" ] ||
        fail "The Node ${required_major} release has no ${platform} build in SHASUMS256.txt.
Install Node ${required_major} from https://nodejs.org/en/download and run this script again."

    checksum=${entry%% *}
    filename=${entry##* }
    version=$(printf '%s' "$filename" | sed -E 's/^node-(v[0-9.]+)-.*$/\1/')

    tmp=$(mktemp -d)
    CLEANUP_DIRS+=("$tmp")

    info "Downloading Node $version for $platform..."
    curl -fsSL -o "$tmp/$filename" "$dist_url/$filename" ||
        fail "Could not download $dist_url/$filename."

    info "Verifying the checksum..."
    (cd "$tmp" && printf '%s  %s\n' "$checksum" "$filename" | shasum -a 256 -c --status -) ||
        fail "The checksum of $filename does not match the one $dist_url/SHASUMS256.txt publishes.
The script stopped rather than install it. Try again, and if it repeats, report it."

    info "Installing to $TOOLS_DIR/node..."
    rm -rf "$TOOLS_DIR/node"
    mkdir -p "$TOOLS_DIR/node"
    tar -xzf "$tmp/$filename" -C "$TOOLS_DIR/node" --strip-components 1

    add_to_path "$TOOLS_DIR/node/bin"
    hash -r
    INSTALLED+=("Node $version in $TOOLS_DIR/node")
}

# Reads engines.node out of package.json without a JSON parser. Every other
# pin goes through json_value, which runs Node. This one cannot: it is the
# fallback for deciding whether Node needs installing in the first place.
engines_node_pin() {
    sed -n '/"engines"/,/}/p' "$PORTAL/package.json" |
        grep '"node"' |
        head -1 |
        sed -E 's/.*"node"[[:space:]]*:[[:space:]]*"([^"]*)".*/\1/'
}

check_node() {
    local required required_major installed installed_major pinned_by

    # .nvmrc is the pin where a checkout has one. A checkout that predates it
    # still states a floor in engines.node, and that is a usable answer, so a
    # branch without the file gets set up rather than a stack trace.
    if [ -f "$PORTAL/.nvmrc" ]; then
        required=$(tr -d ' \t\r\nv' < "$PORTAL/.nvmrc")
        pinned_by=".nvmrc"
    else
        required=$(engines_node_pin)
        pinned_by="engines.node in package.json"
    fi

    [ -n "$required" ] ||
        fail "Neither .nvmrc nor engines.node in package.json states a Node version.
$PORTAL does not look like a checkout of this repository."

    required_major=$(major_of "$required")

    step "Checking Node ($pinned_by pins $required)"

    installed=$(node --version 2>/dev/null || true)
    installed_major=$(major_of "$installed")

    if [ -n "$installed_major" ] && [ "$installed_major" -ge "$required_major" ] 2>/dev/null; then
        info "Node $installed satisfies the pin."
        return 0
    fi

    if [ -n "$installed" ]; then
        info "Node $installed is installed, and this repository needs version ${required_major} or later."
    else
        info "Node is not installed, and this repository needs version ${required_major} or later."
    fi

    if confirm "Download Node ${required_major} to $TOOLS_DIR/node? It needs no sudo and leaves any other Node alone."; then
        install_node "$required_major"
        installed=$(node --version 2>/dev/null || true)
        installed_major=$(major_of "$installed")
        [ -n "$installed_major" ] && [ "$installed_major" -ge "$required_major" ] 2>/dev/null ||
            fail "Node $installed is on PATH after the install, and the pin asks for ${required_major} or later."
        info "Node $installed satisfies the pin."
        return 0
    fi

    fail "Node ${required_major} or later is needed. Install it and run this script again:
  macOS         brew install node
  Windows       winget install OpenJS.NodeJS
  any platform  https://nodejs.org/en/download"
}

# --- pnpm ------------------------------------------------------------------

# npm, not corepack. Node stopped shipping corepack in its tarball as of Node
# 25, so on a machine this script just set up there is no corepack to call. The
# --prefix keeps the install in a directory this script owns, which is what
# makes it work without sudo whether Node came from here, Homebrew, or a
# platform package.
install_pnpm() {
    local required_major="$1" version

    command -v npm >/dev/null 2>&1 ||
        fail "npm is not on PATH, so the script cannot install pnpm for you.
Install pnpm ${required_major} yourself and run this script again: https://pnpm.io/installation"

    info "Resolving the newest pnpm ${required_major} release..."
    version=$(npm view "pnpm@^${required_major}" version --json 2>/dev/null |
        node -e '
          let raw = "";
          process.stdin.on("data", chunk => raw += chunk).on("end", () => {
            const parsed = JSON.parse(raw || "null");
            const versions = Array.isArray(parsed) ? parsed : [parsed];
            const latest = versions.filter(Boolean).pop();
            if (!latest) { process.exit(1); }
            process.stdout.write(String(latest));
          });
        ') ||
        fail "Could not read the pnpm versions from the npm registry.
Install pnpm ${required_major} yourself and run this script again: https://pnpm.io/installation"

    info "Installing pnpm $version to $TOOLS_DIR..."
    mkdir -p "$TOOLS_DIR"
    npm install -g --prefix "$TOOLS_DIR" "pnpm@$version" ||
        fail "npm could not install pnpm@$version into $TOOLS_DIR."

    add_to_path "$TOOLS_DIR/bin"
    hash -r
    INSTALLED+=("pnpm $version in $TOOLS_DIR")
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

    if [ -n "$installed" ]; then
        info "pnpm $installed is installed, and this repository needs version ${required_major} or later."
    else
        info "pnpm is not installed, and this repository needs version ${required_major} or later."
    fi

    if confirm "Install pnpm ${required_major} to $TOOLS_DIR? It needs no sudo."; then
        install_pnpm "$required_major"
        installed=$(pnpm --version 2>/dev/null || true)
        installed_major=$(major_of "$installed")
        [ -n "$installed_major" ] && [ "$installed_major" -ge "$required_major" ] 2>/dev/null ||
            fail "pnpm $installed is on PATH after the install, and the pin asks for ${required_major} or later."
        info "pnpm $installed satisfies the pin."
        return 0
    fi

    fail "pnpm ${required_major} or later is needed. Install it and run this script again:
  npm install -g pnpm@${required_major}
  or read https://pnpm.io/installation"
}

# --- .NET ------------------------------------------------------------------

# dotnet-install.sh is the installer Microsoft publishes for exactly this case:
# a versioned SDK in a directory of your choosing, with no package manager and
# no sudo.
install_dotnet() {
    local pinned="$1" tmp

    tmp=$(mktemp -d)
    CLEANUP_DIRS+=("$tmp")

    info "Downloading the .NET install script..."
    curl -fsSL -o "$tmp/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh ||
        fail "Could not download https://dot.net/v1/dotnet-install.sh.
Install the .NET SDK $pinned from https://dotnet.microsoft.com/download and run this script again."
    chmod +x "$tmp/dotnet-install.sh"

    info "Installing the .NET SDK $pinned to $DOTNET_DIR..."
    "$tmp/dotnet-install.sh" --version "$pinned" --install-dir "$DOTNET_DIR" ||
        fail "The .NET install script failed for version $pinned."

    export DOTNET_ROOT="$DOTNET_DIR"
    add_to_path "$DOTNET_DIR"
    hash -r
    INSTALLED+=(".NET SDK $pinned in $DOTNET_DIR")
}

check_dotnet() {
    local pinned
    pinned=$(json_value "$PORTAL/global.json" sdk.version) ||
        fail "global.json has no sdk.version entry, so the .NET SDK version cannot be read."

    step "Checking the .NET SDK (global.json pins $pinned)"

    # `dotnet --version` inside the checkout resolves global.json, including its
    # rollForward. A zero exit is therefore the whole check: it says an SDK is
    # installed and that this repository accepts it.
    if command -v dotnet >/dev/null 2>&1 && (cd "$PORTAL" && dotnet --version >/dev/null 2>&1); then
        info ".NET SDK $(cd "$PORTAL" && dotnet --version) satisfies global.json."
        return 0
    fi

    if command -v dotnet >/dev/null 2>&1; then
        info "No installed .NET SDK satisfies global.json, which asks for $pinned. Installed SDKs:"
        dotnet --list-sdks 2>/dev/null | sed 's/^/      /'
    else
        info "The .NET SDK is not installed, and global.json asks for $pinned."
    fi

    if confirm "Download the .NET SDK $pinned to $DOTNET_DIR? It needs no sudo and leaves any other SDK alone."; then
        install_dotnet "$pinned"
        (cd "$PORTAL" && dotnet --version >/dev/null 2>&1) ||
            fail "The .NET SDK in $DOTNET_DIR does not satisfy global.json after the install."
        info ".NET SDK $(cd "$PORTAL" && dotnet --version) satisfies global.json."
        return 0
    fi

    fail "The .NET SDK $pinned is needed. Install it and run this script again:
  macOS         brew install dotnet
  Windows       winget install Microsoft.DotNet.SDK.10
  any platform  https://dotnet.microsoft.com/download"
}

# --- container runtime -----------------------------------------------------

responding_runtime() {
    local runtime
    for runtime in docker podman nerdctl; do
        command -v "$runtime" >/dev/null 2>&1 || continue
        if "$runtime" info >/dev/null 2>&1; then
            printf '%s' "$runtime"
            return 0
        fi
    done
    return 1
}

# Starts the Podman VM, and creates it first when this is a new install. macOS
# and Windows always need one. Linux runs containers natively, so a missing
# `podman machine` there is normal and not an error.
start_podman_machine() {
    if podman info >/dev/null 2>&1; then
        return 0
    fi

    if [ "$(uname -s)" = "Linux" ]; then
        return 1
    fi

    if ! podman machine inspect >/dev/null 2>&1; then
        info "Creating the Podman virtual machine. This takes a few minutes the first time..."
        podman machine init || return 1
    fi

    info "Starting the Podman virtual machine..."
    podman machine start || return 1
    podman info >/dev/null 2>&1
}

install_podman() {
    case "$(uname -s)" in
        Darwin)
            command -v brew >/dev/null 2>&1 ||
                fail "Homebrew is not installed, so the script cannot install Podman for you.
Install Podman from https://podman.io/docs/installation and run this script again."
            info "Installing Podman with Homebrew..."
            brew install podman || fail "brew install podman failed."
            ;;
        Linux)
            # Every one of these writes outside the home directory, so each one
            # needs sudo. The script asks for the runtime, and sudo asks for the
            # password itself.
            if command -v apt-get >/dev/null 2>&1; then
                info "Installing Podman with apt. It asks for your password..."
                sudo apt-get update && sudo apt-get install -y podman || fail "apt-get install podman failed."
            elif command -v dnf >/dev/null 2>&1; then
                info "Installing Podman with dnf. It asks for your password..."
                sudo dnf install -y podman || fail "dnf install podman failed."
            else
                fail "The script does not know the package manager on this system.
Install Podman from https://podman.io/docs/installation and run this script again."
            fi
            ;;
        *)
            fail "The script has no Podman install for $(uname -s).
Install it from https://podman.io/docs/installation and run this script again."
            ;;
    esac

    hash -r
    start_podman_machine ||
        fail "Podman is installed, and its virtual machine did not start.
Run 'podman machine init' and 'podman machine start' by hand, then run this script again."
    INSTALLED+=("Podman, with its virtual machine started")
}

check_container_runtime() {
    step "Checking for a container runtime"

    local runtime
    if runtime=$(responding_runtime); then
        info "$runtime is installed and responding."
        return 0
    fi

    # An installed but stopped runtime is a different problem from an absent
    # one, and it has a different fix, so the two get different treatment.
    if command -v podman >/dev/null 2>&1; then
        info "Podman is installed and not responding."
        if confirm "Start the Podman virtual machine?"; then
            start_podman_machine && { info "Podman is responding."; return 0; }
            fail "The Podman virtual machine did not start.
Run 'podman machine start' by hand and read its output, then run this script again."
        fi
        fail "The local stack needs a container runtime for the databases, Redis, Keycloak, and Mailpit.
Start Podman and run this script again:
  podman machine start"
    fi

    if command -v docker >/dev/null 2>&1; then
        fail "Docker is installed and it is not responding.
Start Docker Desktop and run this script again. The local stack needs it for the databases, Redis, Keycloak, and Mailpit."
    fi

    info "No container runtime is installed, and the local stack needs one for the databases, Redis, Keycloak, and Mailpit."
    if confirm "Install Podman? It is the usual choice here, because Docker Desktop licensing is a problem for some of us."; then
        install_podman
        info "Podman is responding."
        return 0
    fi

    fail "A container runtime is needed. Install one and run this script again:
  Podman        https://podman.io/docs/installation   (then: podman machine init && podman machine start)
  Docker        https://www.docker.com/products/docker-desktop"
}

# --- Aspire ----------------------------------------------------------------

# The CLI comes from pnpm, so it lands in a user directory like everything else
# above. PNPM_HOME is set here rather than through `pnpm setup`, because that
# command edits a shell profile, and this script asks before it does that.
check_aspire() {
    local pinned installed

    # A checkout without this file has no AppHost to run, so there is nothing
    # for the CLI to do and its absence is not a failure.
    if [ ! -f "$PORTAL/aspire.config.json" ]; then
        step "Checking the Aspire CLI"
        info "This checkout has no aspire.config.json, so it has no AppHost. Skipping the CLI."
        return 0
    fi

    pinned=$(json_value "$PORTAL/aspire.config.json" sdk.version) ||
        fail "aspire.config.json has no sdk.version entry, so the Aspire CLI version cannot be read."

    step "Checking the Aspire CLI (aspire.config.json pins $pinned)"

    # An earlier run of this script puts the CLI here, and that directory is not
    # on PATH in a new terminal until the profile block goes in. Adding it for
    # the lookup finds that install. It is added only when it holds a CLI, so a
    # machine that gets the CLI from somewhere else does not collect an empty
    # directory in its profile.
    local fallback_home="$TOOLS_DIR/pnpm-global"
    if ! command -v aspire >/dev/null 2>&1 && [ -x "$fallback_home/aspire" ]; then
        export PNPM_HOME="${PNPM_HOME:-$fallback_home}"
        add_to_path "$fallback_home"
        hash -r
    fi

    # `aspire --version` prints the version with build metadata attached, as in
    # 13.5.4+9c1b401. The pin in aspire.config.json carries no metadata, so the
    # comparison is on the part before the plus sign.
    installed=$(aspire --version 2>/dev/null | head -1 | tr -d ' \t\r' | cut -d+ -f1 || true)
    if [ "$installed" = "$pinned" ]; then
        info "The Aspire CLI $installed matches the pin."
        ASPIRE_READY=1
        return 0
    fi

    if [ -n "$installed" ]; then
        info "The Aspire CLI $installed is installed, and aspire.config.json pins $pinned."
    else
        info "The Aspire CLI is not installed."
    fi

    # pnpm needs PNPM_HOME to place a global binary. Setting it here rather than
    # running `pnpm setup` keeps the profile edit in one place, at the end of
    # the run, where the script asks before it writes.
    export PNPM_HOME="${PNPM_HOME:-$fallback_home}"

    if confirm "Install the Aspire CLI $pinned with pnpm, into $PNPM_HOME?"; then
        mkdir -p "$PNPM_HOME"
        pnpm add -g "@microsoft/aspire-cli@$pinned" ||
            fail "pnpm could not install @microsoft/aspire-cli@$pinned."
        add_to_path "$PNPM_HOME"
        hash -r
        INSTALLED+=("Aspire CLI $pinned in $PNPM_HOME")
        ASPIRE_READY=1
        return 0
    fi

    # The Compose path does not need this, so a no here is a choice and not a
    # failure. The summary says which paths are open.
    notice "The Aspire CLI is not installed. The Compose path works without it, and 'pnpm aspire:dc' does not."
    return 0
}

check_certificates() {
    [ "$ASPIRE_READY" -eq 1 ] || return 0

    step "Checking the developer certificate"

    # `aspire certs` can trust a certificate and it cannot report on one, so the
    # check goes through the .NET SDK. This is the same command the AppHost runs
    # in capabilities/developer-certificate.mts, so the two agree on the answer.
    if dotnet dev-certs https --check --trust >/dev/null 2>&1; then
        info "A trusted developer certificate is in place."
        CERTS_TRUSTED=1
        return 0
    fi

    info "No trusted developer certificate. Aspire gives Redis and Keycloak TLS with it."
    # Trusting a certificate writes to the login keychain, and that prompt needs
    # a person. An unattended run therefore has to skip it, and the summary says
    # so rather than letting Aspire fall back to a plain endpoint in silence.
    if [ "$ASSUME_YES" -eq 1 ] && [ ! -r /dev/tty ]; then
        notice "Trusting the certificate needs a keychain prompt, so an unattended run cannot do it. Run 'aspire certs trust' yourself."
        return 0
    fi

    if confirm "Trust it now? Your keychain asks for a password."; then
        if aspire certs trust; then
            info "The developer certificate is trusted."
            CERTS_TRUSTED=1
            INSTALLED+=("A trusted developer certificate")
        else
            notice "'aspire certs trust' did not finish. Run it yourself before you start Aspire."
        fi
        return 0
    fi

    notice "The developer certificate is not trusted. Redis gets a plain endpoint, and Aspire shows no error when that happens."
}

# --- PATH ------------------------------------------------------------------

profile_file() {
    case "$(basename "${SHELL:-}")" in
        zsh)  printf '%s' "$HOME/.zshrc" ;;
        bash)
            # macOS login shells read .bash_profile and Linux reads .bashrc.
            # Writing to the one that already exists keeps the script out of
            # that argument.
            if [ -f "$HOME/.bashrc" ]; then
                printf '%s' "$HOME/.bashrc"
            else
                printf '%s' "$HOME/.bash_profile"
            fi
            ;;
        *) return 1 ;;
    esac
}

persist_path() {
    [ ${#PATH_ADDITIONS[@]} -gt 0 ] || return 0

    local profile block marker="# >>> sebt portal workspace >>>"

    step "Making this run's PATH permanent"
    info "This run added these directories to PATH:"
    printf '      %s\n' "${PATH_ADDITIONS[@]}"
    info "A new terminal does not have them, so 'pnpm aspire:dc' would not find these tools."

    if ! profile=$(profile_file); then
        notice "The script does not know the profile file for ${SHELL:-your shell}. Add the directories above to PATH yourself."
        return 0
    fi

    if [ -f "$profile" ] && grep -qF "$marker" "$profile"; then
        info "$profile already has the block from an earlier run. Leaving it as it is."
        return 0
    fi

    if ! confirm "Add them to $profile?"; then
        notice "PATH is unchanged. This run works, and a new terminal needs the directories above on PATH."
        return 0
    fi

    block=$(printf '\n%s\n' "$marker")
    for dir in "${PATH_ADDITIONS[@]}"; do
        block=$(printf '%s\nexport PATH="%s:$PATH"' "$block" "$dir")
    done
    if [ -n "${PNPM_HOME:-}" ]; then
        block=$(printf '%s\nexport PNPM_HOME="%s"' "$block" "$PNPM_HOME")
    fi
    if [ -d "$DOTNET_DIR" ]; then
        block=$(printf '%s\nexport DOTNET_ROOT="%s"' "$block" "$DOTNET_DIR")
    fi
    block=$(printf '%s\n# <<< sebt portal workspace <<<\n' "$block")

    printf '%s' "$block" >> "$profile"
    info "Added the block to $profile. Open a new terminal, or run: source $profile"
}

summary() {
    printf '\n'
    printf '%s\n' "----------------------------------------------------------------------"
    printf 'Workspace ready: %s\n' "$WORKSPACE"
    printf '%s\n' "----------------------------------------------------------------------"
    printf '\n'
    # The branch decides which versions were read and whether there is an
    # AppHost at all, so it belongs in the summary and not only in git.
    printf 'Checkout\n'
    printf '  Portal      %s on %s\n' "$PORTAL" \
        "$(git -C "$PORTAL" rev-parse --abbrev-ref HEAD 2>/dev/null || printf 'an unknown branch')"
    printf '\n'
    printf 'Toolchain\n'
    printf '  Node        %s\n' "$(node --version)"
    printf '  pnpm        %s\n' "$(pnpm --version)"
    printf '  .NET SDK    %s\n' "$(cd "$PORTAL" && dotnet --version)"
    if [ "$ASPIRE_READY" -eq 1 ]; then
        printf '  Aspire CLI  %s\n' "$(aspire --version 2>/dev/null | head -1)"
    else
        printf '  Aspire CLI  not installed\n'
    fi
    printf '  Containers  %s\n' "$(responding_runtime || printf 'none responding')"
    printf '\n'

    if [ ${#INSTALLED[@]} -gt 0 ]; then
        printf 'Installed\n'
        printf '  %s\n' "${INSTALLED[@]}"
        printf '\n'
    fi

    if [ "$DC_CONNECTOR_PRESENT" -eq 1 ]; then
        printf 'States      DC and CO are both ready.\n'
    else
        printf 'States      CO only. You cannot run the DC configuration.\n'
        printf '            The DC connector is not at\n'
        printf '            %s.\n' "$DC_CONNECTOR"
        printf '            It is a private repository, so the clone needs your GitHub access.\n'
        printf '            Clone it beside the portal, or point DC_CONNECTOR_PATH at your\n'
        printf '            checkout, then run this script again.\n'
    fi
    printf '\n'

    printf 'Next\n'
    printf '  cd %s\n' "$PORTAL"
    if [ "$ASPIRE_READY" -eq 1 ]; then
        if [ "$DC_CONNECTOR_PRESENT" -eq 1 ]; then
            printf '  pnpm aspire:dc      start DC: portal database, DcSource, its seed job, the plugin build, Mailpit\n'
        fi
        printf '  pnpm aspire:co      start CO: portal database, Redis with 2 user interfaces, Keycloak\n'
        if [ "$CERTS_TRUSTED" -eq 0 ]; then
            printf '\n'
            printf '  Run "aspire certs trust" first. Without it Redis gets a plain endpoint\n'
            printf '  and Aspire reports no error.\n'
        fi
    else
        printf '\n'
        printf '  The Aspire CLI is not installed, so use the Compose path. README.md covers it\n'
        printf '  under "Start services" and "Build and run the app".\n'
    fi
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
if ! clone_repo "$portal_url" "$PORTAL" "The portal"; then
    # Never advise a step this run already took. The retry above turns
    # --system-certs on by itself, so by the time a certificate error reaches
    # here, the trust store has been tried and did not hold the root.
    if [ "$SYSTEM_CERTS" -eq 1 ]; then
        tls_advice="The system trust store was already tried, and it does not hold the root
      your proxy presents. Export that root to a file and pass
      --ca-bundle <file>."
    else
        tls_advice="A proxy is inspecting TLS. Re-run with --system-certs."
    fi

    fail "Could not clone the portal from $portal_url.

This is what git said:

$(printf '%s' "$CLONE_OUTPUT" | sed 's/^/  /')

Read that first. These are the usual causes, and the words above decide which:

  'SSL certificate problem', 'unable to get local issuer certificate',
  'SSL peer certificate ... was not OK'
      $tls_advice

  'Could not resolve host', 'Failed to connect', 'Connection timed out'
      No route to github.com. This needs a proxy or a VPN, and no flag here
      configures one. Set https_proxy in your environment and try again.

  'Authentication failed', 'Repository not found', 'terminal prompts disabled'
      HTTPS access is the problem. Re-run with --ssh to clone over SSH.

  'already exists and is not an empty directory'
      Remove $PORTAL and run this script again."
fi

# Every version below comes out of the checkout above, so nothing here runs
# before the clone.
check_node
check_pnpm
check_dotnet
check_container_runtime

step "Cloning the DC connector"
# DC is the one state whose connector lives outside this repository, and it is
# private, so a developer without access to it still has a working CO
# workspace. A failure here is a warning and a line in the summary, and not the
# end of the run.
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

check_aspire
check_certificates
persist_path

summary
