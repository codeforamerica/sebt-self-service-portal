#Requires -Version 5.1
<#
.SYNOPSIS
    Prepare a workspace for local development on the SEBT portal.

.DESCRIPTION
    The script builds the two-repository layout the portal expects, puts the
    toolchain the repository pins in place, installs the dependencies, builds the
    solution, and installs the Aspire CLI. It ends at the point where one command
    starts the app.

    It asks before it installs anything, and it installs into your user profile.
    Node goes to the tools directory below and the .NET SDK goes to
    $env:USERPROFILE\.dotnet, so a machine-wide Node or .NET stays as it is, and
    no step needs an elevated prompt. Decline any offer and the script prints the
    command that does that step by hand.

    No version is written here. Each one comes from a config file in the portal
    checkout, so a bump to that file is the only edit a bump needs:

      .nvmrc             Node
      package.json       pnpm, from engines.pnpm
      global.json        the .NET SDK
      aspire.config.json the Aspire CLI, from sdk.version

    Thus the script clones the portal before it reads a version. Git is the one
    prerequisite it cannot install, because it needs git to reach the file that
    holds the others.

    Podman is the exception to the pinned-download rule. The repository pins no
    container runtime, and Podman has no user-local archive worth maintaining, so
    that one offer goes through winget.

    The macOS and Linux twin of this script is init-workspace.sh. Keep the two in
    step.

.PARAMETER Workspace
    Parent directory that holds both repositories. Defaults to the parent of this
    checkout when the script runs from inside one, and to .\sebt-portal-workspace
    otherwise.

.PARAMETER Branch
    Clone this branch of the portal rather than the default one. Use this to set
    up from a branch whose changes have not merged yet. It applies to the portal
    only. The DC connector is a separate repository and always comes from its
    default branch.

.PARAMETER Ssh
    Clone over SSH instead of HTTPS.

.PARAMETER Yes
    Accept every install offer without asking. Use this for an unattended run.

.PARAMETER CheckOnly
    Decline every install offer. The script still clones, installs the JavaScript
    dependencies, and builds the solution, and it stops at the first tool it needs
    and does not have.

.PARAMETER SystemCerts
    Behind a firewall that inspects TLS, use this one flag. It points git, Node,
    pnpm, and .NET at the Windows certificate store your machine already has.

.PARAMETER CaBundle
    Use an explicit PEM bundle rather than the certificate store. Implies
    -SystemCerts. Reach for this only when the proxy root is a file that was never
    added to the store.

.EXAMPLE
    .\init-workspace.ps1

.EXAMPLE
    .\init-workspace.ps1 -Workspace C:\src\sebt -Yes
#>

[CmdletBinding()]
param(
    [Alias('w')]
    [string] $Workspace,

    [string] $Branch,

    [switch] $Ssh,

    [Alias('y')]
    [switch] $Yes,

    [switch] $CheckOnly,

    [switch] $SystemCerts,

    [string] $CaBundle
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PortalDirName = 'sebt-self-service-portal'
$DcDirName = 'sebt-self-service-portal-dc-connector'

$PortalRepo = if ($Ssh) { "git@github.com:codeforamerica/$PortalDirName.git" }
              else { "https://github.com/codeforamerica/$PortalDirName.git" }
$DcRepo = if ($Ssh) { "git@github.com:codeforamerica/$DcDirName.git" }
          else { "https://github.com/codeforamerica/$DcDirName.git" }

# Everything the script installs for Node lands here. One directory keeps the
# whole footprint visible, and removing it undoes every Node-side change.
$ToolsDir = if ($env:SEBT_TOOLS_DIR) { $env:SEBT_TOOLS_DIR }
            else { Join-Path $env:LOCALAPPDATA 'sebt' }
$DotnetDir = if ($env:DOTNET_INSTALL_DIR) { $env:DOTNET_INSTALL_DIR }
             else { Join-Path $env:USERPROFILE '.dotnet' }

$script:DcConnectorPresent = $false
# Set when the run settles on Podman, so Aspire is told to use it.
$script:ContainerRuntimeChoice = ''
$script:AspireReady = $false
$script:CertsTrusted = $false
# What git said about the last clone, so a failure can quote it.
$script:CloneOutput = ''
# Arguments put in front of every git subcommand, such as the TLS backend.
$script:GitExtraArgs = @()
# Directories this run put on PATH. The summary offers to make them permanent.
$script:PathAdditions = @()
# One line per thing the script installed, for the summary.
$script:Installed = @()

if ($Yes -and $CheckOnly) {
    Write-Host "`nERROR: -Yes and -CheckOnly ask for opposite things. Pass one of them.`n" -ForegroundColor Red
    exit 1
}

function Write-Step { param([string] $Message) Write-Host "`n==> $Message" }
function Write-Info { param([string] $Message) Write-Host "    $Message" }

# Reports a problem the script is not going to stop for. Every one of these has a
# matching paragraph in the summary, so the message here stays short.
function Write-Notice {
    param([string] $Message)
    Write-Host "    WARNING: $Message"
}

function Stop-WithError {
    param([string] $Message)
    Write-Host "`nERROR: $Message`n" -ForegroundColor Red
    exit 1
}

function Test-CommandExists {
    param([string] $Name)
    return [bool] (Get-Command $Name -ErrorAction SilentlyContinue)
}

<#
Every native command in this script goes through one of the three wrappers below,
and none of them is called directly.

This script runs with $ErrorActionPreference = 'Stop', and under that setting
PowerShell turns anything a native command writes to stderr into a terminating
NativeCommandError. Ordinary tools write to stderr for ordinary reasons: git
clone reports progress there, `git rev-parse` outside a repository says so there,
pnpm prints its progress there, and `dotnet --version` complains there when no
SDK matches global.json. Redirecting with 2>$null does not help, because the
error record is created before the redirection discards the text.

So each wrapper lowers the preference for the length of the call and reads the
exit code instead. Exit codes are what these tools use to report failure anyway.
#>

# Runs a native command and returns its standard output, or $null when the
# command is absent or exits non-zero. This is the one for version lookups.
function Get-CommandOutput {
    param(
        [Parameter(Mandatory)] [string] $Command,
        [string[]] $Arguments = @()
    )

    if (-not (Test-CommandExists $Command)) { return $null }

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & $Command @Arguments 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
        $text = ($output | Out-String).Trim()
        if ([string]::IsNullOrWhiteSpace($text)) { return $null }
        return $text
    } catch {
        return $null
    } finally {
        $ErrorActionPreference = $previous
    }
}

# Runs a native command for its exit code alone and discards every stream. This
# is the one for probes such as `podman info`.
function Test-NativeSuccess {
    param(
        [Parameter(Mandatory)] [string] $Command,
        [string[]] $Arguments = @()
    )

    if (-not (Test-CommandExists $Command)) { return $false }

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Command @Arguments *> $null
        return ($LASTEXITCODE -eq 0)
    } catch {
        return $false
    } finally {
        $ErrorActionPreference = $previous
    }
}

# Runs a native command with its output on screen and returns whether it
# succeeded. This is the one for work a developer should watch, such as a clone
# or an install.
function Invoke-NativeStatus {
    param(
        [Parameter(Mandatory)] [string] $Command,
        [string[]] $Arguments = @()
    )

    if (-not (Test-CommandExists $Command)) { return $false }

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Command @Arguments
        return ($LASTEXITCODE -eq 0)
    } catch {
        return $false
    } finally {
        $ErrorActionPreference = $previous
    }
}

# Runs a native command and stops the script when it fails. PowerShell does not
# do this on its own, so a failure otherwise reaches the summary as a success.
function Invoke-Native {
    param(
        [Parameter(Mandatory)] [string] $Command,
        [string[]] $Arguments = @(),
        [string] $ErrorMessage
    )

    if (-not (Invoke-NativeStatus $Command $Arguments)) {
        if (-not $ErrorMessage) {
            $ErrorMessage = "``$Command $($Arguments -join ' ')`` failed with exit code $LASTEXITCODE."
        }
        Stop-WithError $ErrorMessage
    }
}

# Asks a yes or no question and answers it for the caller under -Yes and
# -CheckOnly. A session with no console to read from counts as a no, so an
# unattended run without -Yes stops rather than hangs.
function Confirm-Action {
    param([string] $Question)

    if ($CheckOnly) {
        Write-Info "Skipping (-CheckOnly): $Question"
        return $false
    }

    if ($Yes) {
        Write-Info "Yes (-Yes): $Question"
        return $true
    }

    if ([Console]::IsInputRedirected) {
        Write-Info "No console to ask on, so treating this as no: $Question"
        return $false
    }

    $reply = Read-Host "    $Question [y/N]"
    return ($reply -match '^(y|yes)$')
}

# Puts a directory at the front of PATH for this run and records it, so the
# summary can offer to make it permanent. A directory that is already on PATH is
# not recorded, because there is nothing there for a profile to fix.
function Add-ToPath {
    param([string] $Directory)

    $current = $env:PATH -split [IO.Path]::PathSeparator
    if ($current -contains $Directory) { return }

    $env:PATH = "$Directory$([IO.Path]::PathSeparator)$env:PATH"
    $script:PathAdditions += $Directory
}

# The leading integer of a version string. `v25.6.1` and `>=10.0.0` both give their
# major, which is the only part any of these pins constrains.
function Get-MajorVersion {
    param([string] $Version)
    if ([string]::IsNullOrWhiteSpace($Version)) { return $null }
    $match = [regex]::Match($Version, '\d+')
    if (-not $match.Success) { return $null }
    return [int] $match.Value
}

function Get-JsonValue {
    param([string] $Path, [string] $Property)
    $value = Get-Content -Raw -Path $Path | ConvertFrom-Json
    foreach ($key in $Property.Split('.')) {
        # Set-StrictMode turns a missing property into a terminating error rather
        # than $null, so the key has to be looked up before it is read. Otherwise
        # a config file that lost an entry raises a PowerShell exception instead
        # of the message the caller wrote for exactly that case.
        if ($null -eq $value -or -not $value.PSObject.Properties[$key]) { return $null }
        $value = $value.$key
    }
    return $value
}

function Set-CertificatePolicy {
    if (-not $SystemCerts -and -not $CaBundle) { return }

    Write-Step 'Trusting the system certificate store'

    # Node reads its own bundle by default, so a proxy root in the Windows
    # certificate store is invisible to it and to pnpm. This is the supported opt
    # in, and it keeps verification on. Never reach for strict-ssl=false here.
    $env:NODE_OPTIONS = ($env:NODE_OPTIONS, '--use-system-ca' | Where-Object { $_ }) -join ' '
    Write-Info 'Node and pnpm now read the operating system trust store.'

    # Git needs telling separately, and it is the first thing that reaches the
    # network here, so a clone fails before Node ever runs. The schannel backend
    # is what makes git read the Windows certificate store, where a corporate
    # proxy root already lives.
    #
    # This goes on the command line rather than into GIT_CONFIG_KEY_0, for two
    # reasons. A -c argument outranks every config file, and it works on git
    # versions older than the 2.31 that added those variables. The env route
    # failed silently on a real machine, and the giveaway was the error text:
    # 'SSL peer certificate ... was not OK' comes from OpenSSL, so git had not
    # switched backends at all. It still leaves the developer's config alone.
    $script:GitExtraArgs = @('-c', 'http.sslBackend=schannel')
    Write-Info 'Git now reads the Windows certificate store, for this run only.'

    if ($CaBundle) {
        if (-not (Test-Path -LiteralPath $CaBundle)) {
            Stop-WithError "CA bundle '$CaBundle' not found."
        }
        $resolved = (Resolve-Path -LiteralPath $CaBundle).Path

        # One file, four consumers: Node adds it to its own bundle, OpenSSL covers
        # .NET where it is used, and curl and git each read their own variable.
        $env:NODE_EXTRA_CA_CERTS = $resolved
        $env:SSL_CERT_FILE = $resolved
        $env:CURL_CA_BUNDLE = $resolved
        $env:GIT_SSL_CAINFO = $resolved
        Write-Info "Added $resolved for Node, .NET, curl, and git."
    }
}

function Resolve-Workspace {
    if ($Workspace) { return $Workspace }

    # Running from inside a checkout is the common case for an existing
    # developer, and the workspace is then the directory that already holds it.
    # Downloaded on its own into an empty directory is the common case for a new
    # one, and git reports that with a failure and a line on stderr. That is an
    # answer and not an error, so the workspace becomes a new directory here.
    $scriptDir = Split-Path -Parent $PSCommandPath
    $checkout = Get-CommandOutput 'git' @('-C', $scriptDir, 'rev-parse', '--show-toplevel')
    if ($checkout) {
        return Split-Path -Parent $checkout
    }

    return (Join-Path (Get-Location) 'sebt-portal-workspace')
}

function Assert-Git {
    Write-Step 'Checking git'
    if (-not (Test-CommandExists 'git')) {
        Stop-WithError @"
git is not installed, and the script needs it to reach every other version this repository pins.
Install it and run this script again:
  winget install Git.Git
  or read https://git-scm.com/downloads
"@
    }
    Write-Info (Get-CommandOutput 'git' @('--version'))
}

# An existing checkout is not cloned again, so -Branch would otherwise be
# ignored without a word, and the run would set itself up against whatever
# branch happened to be there. That is how a developer ends up on main
# wondering where the AppHost went.
function Confirm-Branch {
    param([string] $Directory, [string] $Label, [string] $BranchName)

    if (-not $BranchName) { return }

    $current = Get-CommandOutput 'git' @('-C', $Directory, 'rev-parse', '--abbrev-ref', 'HEAD')
    if ($current -eq $BranchName) {
        Write-Info "$Label is on $BranchName."
        return
    }

    $shown = if ($current) { $current } else { 'an unknown branch' }
    Write-Notice "$Label is on $shown, and -Branch asked for $BranchName."
    if (Confirm-Action "Switch $Label to ${BranchName}?") {
        # The refspec is explicit because a clone made with --depth or
        # --single-branch tracks one branch only, and a bare fetch of the branch
        # name then leaves nothing for checkout to resolve.
        $refspec = "${BranchName}:refs/remotes/origin/$BranchName"
        if (-not (Invoke-NativeStatus 'git' ($script:GitExtraArgs + @('-C', $Directory, 'fetch', 'origin', $refspec)))) {
            Stop-WithError @"
Could not fetch $BranchName into $Directory.
Fetch it by hand and run this script again.
"@
        }

        # The first form moves to a local branch that already exists. The second
        # creates one that follows the remote.
        $checked = Test-NativeSuccess 'git' @('-C', $Directory, 'checkout', $BranchName)
        if (-not $checked) {
            $checked = Invoke-NativeStatus 'git' @('-C', $Directory, 'checkout', '-b', $BranchName, '--track', "origin/$BranchName")
        }
        if (-not $checked) {
            Stop-WithError @"
Fetched $BranchName, and could not check it out in $Directory.
Check for local changes in the way, then run this script again.
"@
        }

        Write-Info "$Label is now on $BranchName."
        return
    }

    Write-Notice "Staying on $shown. Every version this script reads comes from there."
}

# The branch is a parameter and not the script-level one, because it applies to
# the portal alone. The DC connector is a different repository with its own
# branches, and a portal branch name means nothing there.
function Copy-Repository {
    param([string] $Url, [string] $Directory, [string] $Label, [string] $BranchName)

    if (Test-Path -LiteralPath (Join-Path $Directory '.git')) {
        Write-Info "$Label is already cloned at $Directory."
        Confirm-Branch $Directory $Label $BranchName
        return $true
    }

    # git clone writes its progress and its errors to stderr, so this tolerates
    # that stream rather than reading a failure into it. 2>&1 merges the two so
    # every line can be shown now and kept for the failure message, which would
    # otherwise have to guess at the cause. A guess sent a developer after the
    # wrong problem once.
    if (Invoke-Clone $Url $Directory $BranchName) { return $true }

    # A certificate complaint almost always means a proxy is inspecting TLS, and
    # the fix is the one -SystemCerts applies. Applying it here too means the
    # common corporate machine needs no flag and no second run. Anything else,
    # such as a name that does not resolve, is not ours to retry.
    if ($script:GitExtraArgs.Count -eq 0 -and (Test-CertificateComplaint $script:CloneOutput)) {
        Write-Notice 'That reads like a proxy inspecting TLS. Trying again with the Windows certificate store.'
        $script:GitExtraArgs = @('-c', 'http.sslBackend=schannel')
        $env:NODE_OPTIONS = ($env:NODE_OPTIONS, '--use-system-ca' | Where-Object { $_ }) -join ' '

        # git removes a directory it created when the clone fails, and an empty
        # one left by anything else would stop the retry before it starts.
        if ((Test-Path -LiteralPath $Directory) -and
            -not (Get-ChildItem -LiteralPath $Directory -Force)) {
            Remove-Item -LiteralPath $Directory -Force
        }

        if (Invoke-Clone $Url $Directory $BranchName) {
            Write-Info 'That worked. The rest of this run uses the certificate store too.'
            return $true
        }
    }

    return $false
}

# True when git's output is complaining about a certificate rather than about
# the network or the repository. Covers both backends: schannel names itself,
# and OpenSSL reports through libcurl with wording of its own.
function Test-CertificateComplaint {
    param([string] $Output)

    if (-not $Output) { return $false }

    $patterns = @(
        'SSL certificate problem',
        'unable to get local issuer certificate',
        'self.signed certificate',
        'SSL peer certificate',
        'schannel',
        'certificate verify failed',
        'unable to access .* SSL'
    )
    foreach ($pattern in $patterns) {
        if ($Output -match $pattern) { return $true }
    }
    return $false
}

function Invoke-Clone {
    param([string] $Url, [string] $Directory, [string] $BranchName)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $arguments = $script:GitExtraArgs + @('clone')
        if ($BranchName) { $arguments += @('--branch', $BranchName) }
        $arguments += @($Url, $Directory)
        $lines = & git @arguments 2>&1 | ForEach-Object {
            $text = $_.ToString()
            Write-Host "    $text"
            $text
        }
        $script:CloneOutput = ($lines -join [Environment]::NewLine)
        return ($LASTEXITCODE -eq 0)
    } catch {
        $script:CloneOutput = $_.Exception.Message
        return $false
    } finally {
        $ErrorActionPreference = $previous
    }
}

# --- Node ------------------------------------------------------------------

function Get-NodePlatform {
    switch ($env:PROCESSOR_ARCHITECTURE) {
        'AMD64' { return 'win-x64' }
        'ARM64' { return 'win-arm64' }
        'x86'   { return 'win-x86' }
        default { return $null }
    }
}

# Downloads the newest release of the pinned Node major and verifies it against
# the checksum file that release publishes.
function Install-Node {
    param([int] $RequiredMajor)

    $platform = Get-NodePlatform
    if (-not $platform) {
        Stop-WithError @"
This script has no Node download for $env:PROCESSOR_ARCHITECTURE.
Install Node $RequiredMajor from https://nodejs.org/en/download and run this script again.
"@
    }

    $distUrl = "https://nodejs.org/dist/latest-v$RequiredMajor.x"

    Write-Info "Resolving the newest Node $RequiredMajor release..."
    try {
        $shasums = (Invoke-WebRequest -Uri "$distUrl/SHASUMS256.txt" -UseBasicParsing).Content
    } catch {
        Stop-WithError @"
Could not reach $distUrl/SHASUMS256.txt.
Check your network, or install Node $RequiredMajor from https://nodejs.org/en/download and run this script again.
"@
    }

    $entry = $shasums -split "`n" |
        Where-Object { $_ -match "\s+node-v$RequiredMajor\.[0-9.]+-$platform\.zip$" } |
        Select-Object -First 1
    if (-not $entry) {
        Stop-WithError @"
The Node $RequiredMajor release has no $platform build in SHASUMS256.txt.
Install Node $RequiredMajor from https://nodejs.org/en/download and run this script again.
"@
    }

    $parts = $entry.Trim() -split '\s+'
    $checksum = $parts[0]
    $filename = $parts[1]
    $version = ($filename -replace '^node-(v[0-9.]+)-.*$', '$1')

    $tmp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    try {
        Write-Info "Downloading Node $version for $platform..."
        $archive = Join-Path $tmp $filename
        Invoke-WebRequest -Uri "$distUrl/$filename" -OutFile $archive -UseBasicParsing

        Write-Info 'Verifying the checksum...'
        $actual = (Get-FileHash -Path $archive -Algorithm SHA256).Hash
        if ($actual -ne $checksum.ToUpperInvariant()) {
            Stop-WithError @"
The checksum of $filename does not match the one $distUrl/SHASUMS256.txt publishes.
The script stopped rather than install it. Try again, and if it repeats, report it.
"@
        }

        $target = Join-Path $ToolsDir 'node'
        Write-Info "Installing to $target..."
        if (Test-Path -LiteralPath $target) { Remove-Item -Recurse -Force $target }
        New-Item -ItemType Directory -Force -Path $target | Out-Null

        # The archive holds one top-level directory, node-v<version>-<platform>.
        # Expanding to a staging directory and moving its contents up is the
        # PowerShell equivalent of tar --strip-components 1.
        $staging = Join-Path $tmp 'unpacked'
        Expand-Archive -Path $archive -DestinationPath $staging -Force
        $root = Get-ChildItem -Path $staging -Directory | Select-Object -First 1
        Get-ChildItem -Path $root.FullName -Force | Move-Item -Destination $target

        Add-ToPath $target
        $script:Installed += "Node $version in $target"
    } finally {
        Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
    }
}

function Test-Node {
    # .nvmrc is the pin where a checkout has one. A checkout that predates it
    # still states a floor in engines.node, and that is a usable answer, so a
    # branch without the file gets set up rather than a stack trace.
    $nvmrc = Join-Path $Portal '.nvmrc'
    if (Test-Path -LiteralPath $nvmrc) {
        $required = (Get-Content -Raw -Path $nvmrc).Trim().TrimStart('v')
        $pinnedBy = '.nvmrc'
    } else {
        $required = Get-JsonValue (Join-Path $Portal 'package.json') 'engines.node'
        $pinnedBy = 'engines.node in package.json'
    }

    if (-not $required) {
        Stop-WithError @"
Neither .nvmrc nor engines.node in package.json states a Node version.
$Portal does not look like a checkout of this repository.
"@
    }

    $requiredMajor = Get-MajorVersion $required

    Write-Step "Checking Node ($pinnedBy pins $required)"

    $installed = Get-CommandOutput 'node' @('--version')
    $installedMajor = Get-MajorVersion $installed

    if ($null -ne $installedMajor -and $installedMajor -ge $requiredMajor) {
        Write-Info "Node $installed satisfies the pin."
        return
    }

    if ($installed) {
        Write-Info "Node $installed is installed, and this repository needs version $requiredMajor or later."
    } else {
        Write-Info "Node is not installed, and this repository needs version $requiredMajor or later."
    }

    if (Confirm-Action "Download Node $requiredMajor to $(Join-Path $ToolsDir 'node')? It leaves any other Node alone.") {
        Install-Node $requiredMajor
        $installed = Get-CommandOutput 'node' @('--version')
        $installedMajor = Get-MajorVersion $installed
        if ($null -eq $installedMajor -or $installedMajor -lt $requiredMajor) {
            Stop-WithError "Node $installed is on PATH after the install, and the pin asks for $requiredMajor or later."
        }
        Write-Info "Node $installed satisfies the pin."
        return
    }

    Stop-WithError @"
Node $requiredMajor or later is needed. Install it and run this script again:
  winget install OpenJS.NodeJS
  or read https://nodejs.org/en/download
"@
}

# --- pnpm ------------------------------------------------------------------

# npm, not corepack. Node stopped shipping corepack in its archive as of Node 25,
# so on a machine this script just set up there is no corepack to call. The
# --prefix keeps the install in a directory this script owns, which is what makes
# it work without an elevated prompt whether Node came from here or from winget.
function Install-Pnpm {
    param([int] $RequiredMajor)

    if (-not (Test-CommandExists 'npm')) {
        Stop-WithError @"
npm is not on PATH, so the script cannot install pnpm for you.
Install pnpm $RequiredMajor yourself and run this script again: https://pnpm.io/installation
"@
    }

    Write-Info "Resolving the newest pnpm $RequiredMajor release..."
    $raw = Get-CommandOutput 'npm' @('view', "pnpm@^$RequiredMajor", 'version', '--json')
    if ($LASTEXITCODE -ne 0 -or -not $raw) {
        Stop-WithError @"
Could not read the pnpm versions from the npm registry.
Install pnpm $RequiredMajor yourself and run this script again: https://pnpm.io/installation
"@
    }

    # npm prints a bare string for one match and an array for several.
    $parsed = $raw | ConvertFrom-Json
    $version = if ($parsed -is [array]) { $parsed[-1] } else { $parsed }

    Write-Info "Installing pnpm $version to $ToolsDir..."
    New-Item -ItemType Directory -Force -Path $ToolsDir | Out-Null
    Invoke-Native 'npm' @('install', '-g', '--prefix', $ToolsDir, "pnpm@$version") `
        -ErrorMessage "npm could not install pnpm@$version into $ToolsDir."

    # npm on Windows puts the shims in the prefix root, not in a bin directory.
    Add-ToPath $ToolsDir
    $script:Installed += "pnpm $version in $ToolsDir"
}

function Test-Pnpm {
    $required = Get-JsonValue (Join-Path $Portal 'package.json') 'engines.pnpm'
    if (-not $required) {
        Stop-WithError 'package.json has no engines.pnpm entry, so the pnpm version cannot be read.'
    }
    $requiredMajor = Get-MajorVersion $required

    Write-Step "Checking pnpm (package.json pins $required)"

    $installed = Get-CommandOutput 'pnpm' @('--version')
    $installedMajor = Get-MajorVersion $installed

    if ($null -ne $installedMajor -and $installedMajor -ge $requiredMajor) {
        Write-Info "pnpm $installed satisfies the pin."
        return
    }

    if ($installed) {
        Write-Info "pnpm $installed is installed, and this repository needs version $requiredMajor or later."
    } else {
        Write-Info "pnpm is not installed, and this repository needs version $requiredMajor or later."
    }

    if (Confirm-Action "Install pnpm $requiredMajor to ${ToolsDir}?") {
        Install-Pnpm $requiredMajor
        $installed = Get-CommandOutput 'pnpm' @('--version')
        $installedMajor = Get-MajorVersion $installed
        if ($null -eq $installedMajor -or $installedMajor -lt $requiredMajor) {
            Stop-WithError "pnpm $installed is on PATH after the install, and the pin asks for $requiredMajor or later."
        }
        Write-Info "pnpm $installed satisfies the pin."
        return
    }

    Stop-WithError @"
pnpm $requiredMajor or later is needed. Install it and run this script again:
  npm install -g pnpm@$requiredMajor
  or read https://pnpm.io/installation
"@
}

# --- .NET ------------------------------------------------------------------

# Runs from inside the checkout, so global.json decides the answer. A $null here
# means no installed SDK satisfies it, which is the same answer the check wants.
function Get-DotnetVersion {
    Push-Location $Portal
    try {
        return (Get-CommandOutput 'dotnet' @('--version'))
    } finally {
        Pop-Location
    }
}

# dotnet-install.ps1 is the installer Microsoft publishes for exactly this case: a
# versioned SDK in a directory of your choosing, with no package manager and no
# elevated prompt.
function Install-Dotnet {
    param([string] $Pinned)

    $tmp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    try {
        Write-Info 'Downloading the .NET install script...'
        $installer = Join-Path $tmp 'dotnet-install.ps1'
        Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing

        Write-Info "Installing the .NET SDK $Pinned to $DotnetDir..."
        & $installer -Version $Pinned -InstallDir $DotnetDir

        $env:DOTNET_ROOT = $DotnetDir
        Add-ToPath $DotnetDir
        $script:Installed += ".NET SDK $Pinned in $DotnetDir"
    } catch {
        Stop-WithError @"
The .NET install failed for version $Pinned.
$($_.Exception.Message)
Install it from https://dotnet.microsoft.com/download and run this script again.
"@
    } finally {
        Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
    }
}

function Test-Dotnet {
    $pinned = Get-JsonValue (Join-Path $Portal 'global.json') 'sdk.version'
    if (-not $pinned) {
        Stop-WithError 'global.json has no sdk.version entry, so the .NET SDK version cannot be read.'
    }

    Write-Step "Checking the .NET SDK (global.json pins $pinned)"

    # `dotnet --version` inside the checkout resolves global.json, including its
    # rollForward. A zero exit is therefore the whole check: it says an SDK is
    # installed and that this repository accepts it.
    if (Test-CommandExists 'dotnet') {
        $version = Get-DotnetVersion
        if ($version) {
            Write-Info ".NET SDK $version satisfies global.json."
            return
        }
        Write-Info "No installed .NET SDK satisfies global.json, which asks for $pinned. Installed SDKs:"
        (Get-CommandOutput 'dotnet' @('--list-sdks')) -split "`n" | ForEach-Object { Write-Host "      $($_.Trim())" }
    } else {
        Write-Info "The .NET SDK is not installed, and global.json asks for $pinned."
    }

    if (Confirm-Action "Download the .NET SDK $pinned to ${DotnetDir}? It leaves any other SDK alone.") {
        Install-Dotnet $pinned
        if (-not (Get-DotnetVersion)) {
            Stop-WithError "The .NET SDK in $DotnetDir does not satisfy global.json after the install."
        }
        Write-Info ".NET SDK $(Get-DotnetVersion) satisfies global.json."
        return
    }

    Stop-WithError @"
The .NET SDK $pinned is needed. Install it and run this script again:
  winget install Microsoft.DotNet.SDK.10
  or read https://dotnet.microsoft.com/download
"@
}

# --- container runtime -----------------------------------------------------

# Aspire prefers Docker when it finds both, and it has no idea which one this
# script just installed. Naming the choice here is what makes `pnpm aspire:dc`
# reach the same runtime the checks passed against.
# Read https://aspire.dev/get-started/prerequisites/.
function Use-Runtime {
    param([string] $Runtime)

    if ($Runtime -ne 'podman') { return }

    $env:ASPIRE_CONTAINER_RUNTIME = 'podman'
    $script:ContainerRuntimeChoice = 'podman'
    Write-Info 'Set ASPIRE_CONTAINER_RUNTIME=podman, so Aspire uses Podman and not Docker.'
}

function Get-RespondingRuntime {
    foreach ($runtime in @('docker', 'podman', 'nerdctl')) {
        if (Test-NativeSuccess $runtime @('info')) { return $runtime }
    }
    return $null
}

# Starts the Podman VM, and creates it first when this is a new install. Windows
# always needs one.
function Start-PodmanMachine {
    if (Test-NativeSuccess 'podman' @('info')) { return $true }

    if (-not (Test-NativeSuccess 'podman' @('machine', 'inspect'))) {
        Write-Info 'Creating the Podman virtual machine. This takes a few minutes the first time...'
        if (-not (Invoke-NativeStatus 'podman' @('machine', 'init'))) { return $false }
    }

    Write-Info 'Starting the Podman virtual machine...'
    if (-not (Invoke-NativeStatus 'podman' @('machine', 'start'))) { return $false }

    return (Test-NativeSuccess 'podman' @('info'))
}

function Install-Podman {
    if (-not (Test-CommandExists 'winget')) {
        Stop-WithError @"
winget is not installed, so the script cannot install Podman for you.
Install Podman from https://podman.io/docs/installation and run this script again.
"@
    }

    Write-Info 'Installing Podman with winget...'
    Invoke-Native 'winget' @('install', '--id', 'RedHat.Podman',
                             '--accept-source-agreements', '--accept-package-agreements') `
        -ErrorMessage 'winget install RedHat.Podman failed.'

    # winget puts podman on the machine PATH, and this process started before
    # that happened. Re-reading both scopes is what makes the command resolve
    # without a new terminal.
    $env:PATH = [Environment]::GetEnvironmentVariable('PATH', 'Machine') +
                [IO.Path]::PathSeparator +
                [Environment]::GetEnvironmentVariable('PATH', 'User')

    if (-not (Start-PodmanMachine)) {
        Stop-WithError @"
Podman is installed, and its virtual machine did not start.
Run 'podman machine init' and 'podman machine start' by hand, then run this script again.
"@
    }
    $script:Installed += 'Podman, with its virtual machine started'
}

function Test-ContainerRuntime {
    Write-Step 'Checking for a container runtime'

    $runtime = Get-RespondingRuntime
    if ($runtime) {
        Write-Info "$runtime is installed and responding."
        Use-Runtime $runtime
        return
    }

    # An installed but stopped runtime is a different problem from an absent one,
    # and it has a different fix, so the two get different treatment.
    if (Test-CommandExists 'podman') {
        Write-Info 'Podman is installed and not responding.'
        if (Confirm-Action 'Start the Podman virtual machine?') {
            if (Start-PodmanMachine) {
                Write-Info 'Podman is responding.'
                Use-Runtime 'podman'
                return
            }
            Stop-WithError @"
The Podman virtual machine did not start.
Run 'podman machine start' by hand and read its output, then run this script again.
"@
        }
        Stop-WithError @"
The local stack needs a container runtime for the databases, Redis, Keycloak, and Mailpit.
Start Podman and run this script again:
  podman machine start
"@
    }

    if (Test-CommandExists 'docker') {
        Stop-WithError @"
Docker is installed and it is not responding.
Start Docker Desktop and run this script again. The local stack needs it for the databases, Redis, Keycloak, and Mailpit.
"@
    }

    Write-Info 'No container runtime is installed, and the local stack needs one for the databases, Redis, Keycloak, and Mailpit.'
    if (Confirm-Action 'Install Podman? It is the usual choice here, because Docker Desktop licensing is a problem for some of us.') {
        Install-Podman
        Write-Info 'Podman is responding.'
        Use-Runtime 'podman'
        return
    }

    Stop-WithError @"
A container runtime is needed. Install one and run this script again:
  Podman        https://podman.io/docs/installation   (then: podman machine init; podman machine start)
  Docker        https://www.docker.com/products/docker-desktop
"@
}

# --- Aspire ----------------------------------------------------------------

function Test-Aspire {
    # A checkout without this file has no AppHost to run, so there is nothing
    # for the CLI to do and its absence is not a failure.
    $config = Join-Path $Portal 'aspire.config.json'
    if (-not (Test-Path -LiteralPath $config)) {
        Write-Step 'Checking the Aspire CLI'
        Write-Info 'This checkout has no aspire.config.json, so it has no AppHost. Skipping the CLI.'
        return
    }

    $pinned = Get-JsonValue $config 'sdk.version'
    if (-not $pinned) {
        Stop-WithError 'aspire.config.json has no sdk.version entry, so the Aspire CLI version cannot be read.'
    }

    Write-Step "Checking the Aspire CLI (aspire.config.json pins $pinned)"

    # An earlier run of this script puts the CLI here, and that directory is not
    # on PATH in a new terminal until the profile block goes in. Adding it for the
    # lookup finds that install. It is added only when it holds a CLI, so a machine
    # that gets the CLI from somewhere else does not collect an empty directory in
    # its profile.
    $fallbackHome = Join-Path $ToolsDir 'pnpm-global'
    if (-not (Test-CommandExists 'aspire') -and (Test-Path -LiteralPath (Join-Path $fallbackHome 'aspire.exe'))) {
        if (-not $env:PNPM_HOME) { $env:PNPM_HOME = $fallbackHome }
        Add-ToPath $fallbackHome
    }

    # `aspire --version` prints the version with build metadata attached, as in
    # 13.5.4+9c1b401. The pin in aspire.config.json carries no metadata, so the
    # comparison is on the part before the plus sign.
    $raw = (Get-CommandOutput 'aspire' @('--version')) -split "`n" | Select-Object -First 1
    $installed = if ($raw) { ($raw.Trim() -split '\+')[0] } else { $null }

    if ($installed -eq $pinned) {
        Write-Info "The Aspire CLI $installed matches the pin."
        $script:AspireReady = $true
        return
    }

    if ($installed) {
        Write-Info "The Aspire CLI $installed is installed, and aspire.config.json pins $pinned."
    } else {
        Write-Info 'The Aspire CLI is not installed.'
    }

    # pnpm needs PNPM_HOME to place a global binary. Setting it here rather than
    # running `pnpm setup` keeps the profile edit in one place, at the end of the
    # run, where the script asks before it writes.
    if (-not $env:PNPM_HOME) { $env:PNPM_HOME = $fallbackHome }

    if (Confirm-Action "Install the Aspire CLI $pinned with pnpm, into ${env:PNPM_HOME}?") {
        New-Item -ItemType Directory -Force -Path $env:PNPM_HOME | Out-Null
        Invoke-Native 'pnpm' @('add', '-g', "@microsoft/aspire-cli@$pinned") `
            -ErrorMessage "pnpm could not install @microsoft/aspire-cli@$pinned."
        Add-ToPath $env:PNPM_HOME
        $script:Installed += "Aspire CLI $pinned in $env:PNPM_HOME"
        $script:AspireReady = $true
        return
    }

    # The Compose path does not need this, so a no here is a choice and not a
    # failure. The summary says which paths are open.
    Write-Notice "The Aspire CLI is not installed. The Compose path works without it, and 'pnpm aspire:dc' does not."
}

function Test-Certificate {
    if (-not $script:AspireReady) { return }

    Write-Step 'Checking the developer certificate'

    # `aspire certs` can trust a certificate and it cannot report on one, so the
    # check goes through the .NET SDK. This is the same command the AppHost runs in
    # capabilities/developer-certificate.mts, so the two agree on the answer.
    if (Test-NativeSuccess 'dotnet' @('dev-certs', 'https', '--check', '--trust')) {
        Write-Info 'A trusted developer certificate is in place.'
        $script:CertsTrusted = $true
        return
    }

    Write-Info 'No trusted developer certificate. Aspire gives Redis and Keycloak TLS with it.'
    # Trusting a certificate opens a Windows prompt, and that needs a person. An
    # unattended run therefore has to skip it, and the summary says so rather than
    # letting Aspire fall back to a plain endpoint in silence.
    if ($Yes -and [Console]::IsInputRedirected) {
        Write-Notice "Trusting the certificate needs a prompt, so an unattended run cannot do it. Run 'aspire certs trust' yourself."
        return
    }

    if (Confirm-Action 'Trust it now? Windows asks you to confirm.') {
        if (Invoke-NativeStatus 'aspire' @('certs', 'trust')) {
            Write-Info 'The developer certificate is trusted.'
            $script:CertsTrusted = $true
            $script:Installed += 'A trusted developer certificate'
        } else {
            Write-Notice "'aspire certs trust' did not finish. Run it yourself before you start Aspire."
        }
        return
    }

    Write-Notice 'The developer certificate is not trusted. Redis gets a plain endpoint, and Aspire shows no error when that happens.'
}

# --- PATH ------------------------------------------------------------------

function Save-Path {
    if ($script:PathAdditions.Count -eq 0 -and -not $script:ContainerRuntimeChoice) { return }

    Write-Step 'Making this run''s PATH permanent'
    Write-Info 'This run added these directories to PATH:'
    $script:PathAdditions | ForEach-Object { Write-Host "      $_" }
    Write-Info "A new terminal does not have them, so 'pnpm aspire:dc' would not find these tools."

    if (-not (Confirm-Action 'Add them to your user PATH?')) {
        Write-Notice 'PATH is unchanged. This run works, and a new terminal needs the directories above on PATH.'
        return
    }

    # The user scope, never the machine scope: this is a per-developer toolchain,
    # and the machine scope needs an elevated prompt.
    $userPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
    $entries = if ($userPath) { $userPath -split ';' | Where-Object { $_ } } else { @() }

    foreach ($directory in $script:PathAdditions) {
        if ($entries -notcontains $directory) { $entries = @($directory) + $entries }
    }
    [Environment]::SetEnvironmentVariable('PATH', ($entries -join ';'), 'User')

    if ($env:PNPM_HOME) {
        [Environment]::SetEnvironmentVariable('PNPM_HOME', $env:PNPM_HOME, 'User')
    }
    if (Test-Path -LiteralPath $DotnetDir) {
        [Environment]::SetEnvironmentVariable('DOTNET_ROOT', $DotnetDir, 'User')
    }
    if ($script:ContainerRuntimeChoice) {
        [Environment]::SetEnvironmentVariable('ASPIRE_CONTAINER_RUNTIME', $script:ContainerRuntimeChoice, 'User')
    }

    Write-Info 'Updated your user PATH. Open a new terminal to pick it up.'
}

function Write-Summary {
    Write-Host ''
    Write-Host '----------------------------------------------------------------------'
    Write-Host "Workspace ready: $WorkspaceRoot"
    Write-Host '----------------------------------------------------------------------'
    Write-Host ''
    # The branch decides which versions were read and whether there is an
    # AppHost at all, so it belongs in the summary and not only in git.
    $portalBranch = Get-CommandOutput 'git' @('-C', $Portal, 'rev-parse', '--abbrev-ref', 'HEAD')
    if (-not $portalBranch) { $portalBranch = 'an unknown branch' }
    Write-Host 'Checkout'
    Write-Host "  Portal      $Portal on $portalBranch"
    Write-Host ''
    Write-Host 'Toolchain'
    Write-Host "  Node        $(Get-CommandOutput 'node' @('--version'))"
    Write-Host "  pnpm        $(Get-CommandOutput 'pnpm' @('--version'))"
    Write-Host "  .NET SDK    $(Get-DotnetVersion)"
    if ($script:AspireReady) {
        # Built up first rather than interpolated inline: Windows PowerShell 5.1
        # is unforgiving about nested double quotes inside a subexpression.
        $aspireVersion = (Get-CommandOutput 'aspire' @('--version')) -split "`n" |
            Select-Object -First 1
        Write-Host "  Aspire CLI  $($aspireVersion.Trim())"
    } else {
        Write-Host '  Aspire CLI  not installed'
    }
    $runtime = Get-RespondingRuntime
    Write-Host "  Containers  $(if ($runtime) { $runtime } else { 'none responding' })"
    Write-Host ''

    if ($script:Installed.Count -gt 0) {
        Write-Host 'Installed'
        $script:Installed | ForEach-Object { Write-Host "  $_" }
        Write-Host ''
    }

    if ($script:DcConnectorPresent) {
        Write-Host 'States      DC and CO are both ready.'
    } else {
        Write-Host 'States      CO only. You cannot run the DC configuration.'
        Write-Host '            The DC connector is not at'
        Write-Host "            $DcConnector."
        Write-Host '            It is a private repository, so the clone needs your GitHub access.'
        Write-Host '            Clone it beside the portal, or point DC_CONNECTOR_PATH at your'
        Write-Host '            checkout, then run this script again.'
    }
    Write-Host ''

    Write-Host 'Next'
    Write-Host "  cd $Portal"
    if ($script:AspireReady) {
        if ($script:DcConnectorPresent) {
            Write-Host '  pnpm aspire:dc      start DC: portal database, DcSource, its seed job, the plugin build, Mailpit'
        }
        Write-Host '  pnpm aspire:co      start CO: portal database, Redis with 2 user interfaces, Keycloak'
        if (-not $script:CertsTrusted) {
            Write-Host ''
            Write-Host '  Run "aspire certs trust" first. Without it Redis gets a plain endpoint'
            Write-Host '  and Aspire reports no error.'
        }
    } else {
        Write-Host ''
        Write-Host '  The Aspire CLI is not installed, so use the Compose path. README.md covers it'
        Write-Host '  under "Start services" and "Build and run the app".'
    }
    Write-Host ''
}

Set-CertificatePolicy
Assert-Git

$WorkspaceRoot = Resolve-Workspace
$Portal = Join-Path $WorkspaceRoot $PortalDirName
$DcConnector = Join-Path $WorkspaceRoot $DcDirName

Write-Step "Preparing the workspace at $WorkspaceRoot"
New-Item -ItemType Directory -Force -Path $WorkspaceRoot | Out-Null

Write-Step 'Cloning the portal'
if (-not (Copy-Repository $PortalRepo $Portal 'The portal' $Branch)) {
    $said = ($script:CloneOutput -split "`n" | ForEach-Object { "  $($_.TrimEnd())" }) -join "`n"

    # Never advise a step this run already took. The retry above turns the
    # certificate store on by itself, so by the time a certificate error
    # reaches here, that has been tried and did not hold the root.
    $tlsAdvice = if ($script:GitExtraArgs.Count -gt 0) {
        @"
The Windows certificate store was already tried, and it does not hold
      the root your proxy presents. Export that root to a file and pass
      -CaBundle <file>.
"@.Trim()
    } else {
        'A proxy is inspecting TLS. Re-run with -SystemCerts.'
    }

    Stop-WithError @"
Could not clone the portal from $PortalRepo.

This is what git said:

$said

Read that first. These are the usual causes, and the words above decide which:

  'SSL certificate problem', 'unable to get local issuer certificate',
  'SSL peer certificate ... was not OK', 'schannel: ...'
      $tlsAdvice

  'Could not resolve host', 'Failed to connect', 'Connection timed out'
      No route to github.com. This needs a proxy or a VPN, and no flag here
      configures one. Set the HTTPS_PROXY environment variable and try again.

  'Authentication failed', 'Repository not found', 'terminal prompts disabled'
      HTTPS access is the problem. Re-run with -Ssh to clone over SSH.

  'already exists and is not an empty directory'
      Remove $Portal and run this script again.
"@
}

# Every version below comes out of the checkout above, so nothing here runs before
# the clone.
Test-Node
Test-Pnpm
Test-Dotnet
Test-ContainerRuntime

Write-Step 'Cloning the DC connector'
# DC is the one state whose connector lives outside this repository, and it is
# private, so a developer without access to it still has a working CO workspace. A
# failure here is a warning and a line in the summary, and not the end of the run.
if (Copy-Repository $DcRepo $DcConnector 'The DC connector') {
    $script:DcConnectorPresent = $true
} else {
    # The summary spells out what this costs, so the message here stays short.
    Write-Notice 'The DC connector could not be cloned. CO will work and DC will not. Read the summary.'
}

Push-Location $Portal
try {
    Write-Step 'Installing JavaScript dependencies'
    Invoke-Native 'pnpm' @('install')

    Write-Step 'Building the .NET solution'
    Invoke-Native 'dotnet' @('build', 'SEBT.slnx')
} finally {
    Pop-Location
}

Test-Aspire
Test-Certificate
Save-Path

Write-Summary
