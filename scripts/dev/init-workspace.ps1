#Requires -Version 5.1
<#
.SYNOPSIS
    Prepare a workspace for local development on the SEBT portal.

.DESCRIPTION
    The script builds the two-repository layout the portal expects, verifies the
    toolchain the repository pins, installs the dependencies, and builds the
    solution. It stops at the point where the app is ready to start.

    It checks the toolchain, and it does not install it. A developer machine holds
    one Node and one .NET for every repository on it, so a setup script is the wrong
    place to change either. Each check therefore reports the version it wants and the
    command that installs it, and the developer runs that command.

    No version is written here. Each one comes from a config file in the portal
    checkout, so a bump to that file is the only edit a bump needs:

      .nvmrc        Node
      package.json  pnpm, from engines.pnpm
      global.json   the .NET SDK

    Thus the script clones the portal before it checks a version. Git is the one
    prerequisite it cannot report on, because it needs git to reach the file that
    holds the others.

    The Aspire CLI is deliberately out of scope. It is a global tool with its own
    one-time steps, including a certificate trust prompt that needs a person. Read
    "Local development with Aspire" in README.md.

    The macOS and Linux twin of this script is init-workspace.sh. Keep the two in step.

.PARAMETER Workspace
    Parent directory that holds both repositories. Defaults to the parent of this
    checkout when the script runs from inside one, and to .\sebt-portal-workspace
    otherwise.

.PARAMETER Ssh
    Clone over SSH instead of HTTPS.

.PARAMETER SystemCerts
    Trust the operating system certificate store for Node and pnpm. Use this behind
    a firewall that inspects TLS.

.PARAMETER CaBundle
    Also trust an explicit PEM bundle. Implies -SystemCerts. Use this when the proxy
    root certificate is a file rather than a certificate store entry.

.EXAMPLE
    .\init-workspace.ps1

.EXAMPLE
    .\init-workspace.ps1 -Workspace C:\src\sebt -SystemCerts
#>

[CmdletBinding()]
param(
    [Alias('w')]
    [string] $Workspace,

    [switch] $Ssh,

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

$script:DcConnectorPresent = $false

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

# Runs a native command and fails the script when it returns a non-zero exit code.
# PowerShell does not do this on its own, so a silent failure otherwise reaches the
# summary as a success.
function Invoke-Native {
    param(
        [Parameter(Mandatory)] [string] $Command,
        [string[]] $Arguments = @(),
        [string] $ErrorMessage
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        if (-not $ErrorMessage) {
            $ErrorMessage = "``$Command $($Arguments -join ' ')`` failed with exit code $LASTEXITCODE."
        }
        Stop-WithError $ErrorMessage
    }
}

function Test-CommandExists {
    param([string] $Name)
    return [bool] (Get-Command $Name -ErrorAction SilentlyContinue)
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

    # Running from inside a checkout is the common case for an existing developer.
    # The workspace is then the directory that already holds it.
    $scriptDir = Split-Path -Parent $PSCommandPath
    $checkout = & git -C $scriptDir rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -eq 0 -and $checkout) {
        return Split-Path -Parent $checkout
    }

    return (Join-Path (Get-Location) 'sebt-portal-workspace')
}

function Assert-Git {
    Write-Step 'Checking git'
    if (-not (Test-CommandExists 'git')) {
        Stop-WithError 'git is not installed. Install it from https://git-scm.com/downloads and run this script again.'
    }
    Write-Info (& git --version)
}

function Copy-Repository {
    param([string] $Url, [string] $Directory, [string] $Label)

    if (Test-Path -LiteralPath (Join-Path $Directory '.git')) {
        Write-Info "$Label is already cloned at $Directory."
        return $true
    }

    & git clone $Url $Directory
    return ($LASTEXITCODE -eq 0)
}

function Test-Node {
    $required = (Get-Content -Raw -Path (Join-Path $Portal '.nvmrc')).Trim().TrimStart('v')
    $requiredMajor = Get-MajorVersion $required

    Write-Step "Checking Node (.nvmrc pins $required)"

    $installed = & node --version 2>$null
    $installedMajor = Get-MajorVersion $installed

    if ($null -ne $installedMajor -and $installedMajor -ge $requiredMajor) {
        Write-Info "Node $installed satisfies the pin."
        return
    }

    if (-not $installed) {
        Stop-WithError @"
Node is not installed, and this repository needs version $requiredMajor or later.
Install it, then run this script again:
  Windows       winget install OpenJS.NodeJS
  any platform  https://nodejs.org/en/download
"@
    }

    Stop-WithError @"
Node $installed is installed, and this repository needs version $requiredMajor or later.
Upgrade it, then run this script again:
  Windows       winget upgrade OpenJS.NodeJS
  any platform  https://nodejs.org/en/download
"@
}

function Test-Pnpm {
    $required = Get-JsonValue (Join-Path $Portal 'package.json') 'engines.pnpm'
    if (-not $required) {
        Stop-WithError 'package.json has no engines.pnpm entry, so the pnpm version cannot be read.'
    }
    $requiredMajor = Get-MajorVersion $required

    Write-Step "Checking pnpm (package.json pins $required)"

    $installed = & pnpm --version 2>$null
    $installedMajor = Get-MajorVersion $installed

    if ($null -ne $installedMajor -and $installedMajor -ge $requiredMajor) {
        Write-Info "pnpm $installed satisfies the pin."
        return
    }

    if (-not $installed) {
        Stop-WithError @"
pnpm is not installed, and this repository needs version $requiredMajor or later.
Install it, then run this script again:
  corepack enable pnpm     (corepack ships with Node, so this needs no download)
  or read https://pnpm.io/installation
"@
    }

    Stop-WithError @"
pnpm $installed is installed, and this repository needs version $requiredMajor or later.
Upgrade it, then run this script again:
  corepack prepare pnpm@latest --activate
  or read https://pnpm.io/installation
"@
}

function Get-DotnetVersion {
    Push-Location $Portal
    try {
        $version = & dotnet --version 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
        return $version
    } catch {
        return $null
    } finally {
        Pop-Location
    }
}

function Test-Dotnet {
    $pinned = Get-JsonValue (Join-Path $Portal 'global.json') 'sdk.version'
    if (-not $pinned) {
        Stop-WithError 'global.json has no sdk.version entry, so the .NET SDK version cannot be read.'
    }

    Write-Step "Checking the .NET SDK (global.json pins $pinned)"

    if (-not (Test-CommandExists 'dotnet')) {
        Stop-WithError @"
The .NET SDK is not installed, and global.json asks for $pinned.
Install it, then run this script again:
  Windows       winget install Microsoft.DotNet.SDK.10
  any platform  https://dotnet.microsoft.com/download
"@
    }

    # `dotnet --version` inside the checkout resolves global.json, including its
    # rollForward. A zero exit is therefore the whole check: it says an SDK is
    # installed and that this repository accepts it.
    $version = Get-DotnetVersion
    if ($version) {
        Write-Info ".NET SDK $version satisfies global.json."
        return
    }

    $sdks = (& dotnet --list-sdks 2>$null | ForEach-Object { "  $_" }) -join "`n"
    Stop-WithError @"
No installed .NET SDK satisfies global.json, which asks for $pinned.
Installed SDKs:
$sdks
Install $pinned or a later patch of it from https://dotnet.microsoft.com/download, then run this script again.
"@
}

function Test-ContainerRuntime {
    Write-Step 'Checking for a container runtime'

    $installed = @()
    foreach ($runtime in @('docker', 'podman', 'nerdctl')) {
        if (-not (Test-CommandExists $runtime)) { continue }
        $installed += $runtime

        & $runtime info *> $null
        if ($LASTEXITCODE -eq 0) {
            Write-Info "$runtime is installed and responding."
            return
        }
    }

    # An installed but stopped runtime is a different problem from an absent one,
    # and it has a different fix, so the two get different messages.
    if ($installed.Count -gt 0) {
        Stop-WithError @"
A container runtime is installed ($($installed -join ', ')) but it is not responding.
Start it and run this script again. The local stack needs it for the databases, Redis, Keycloak, and Mailpit.
"@
    }

    Stop-WithError @"
No container runtime is installed, and the local stack cannot start the databases, Redis, Keycloak, or Mailpit without one.
Install one and run this script again:
  Podman        https://podman.io/docs/installation   (then: podman machine init; podman machine start)
  Docker        https://www.docker.com/products/docker-desktop
Podman is the usual choice where Docker Desktop licensing is a problem.
"@
}

function Write-Summary {
    Write-Host ''
    Write-Host '----------------------------------------------------------------------'
    Write-Host "Workspace ready: $WorkspaceRoot"
    Write-Host '----------------------------------------------------------------------'
    Write-Host ''
    Write-Host 'Toolchain'
    Write-Host "  Node        $(& node --version)"
    Write-Host "  pnpm        $(& pnpm --version)"
    Write-Host "  .NET SDK    $(Get-DotnetVersion)"
    Write-Host ''

    if ($script:DcConnectorPresent) {
        Write-Host 'States      DC and CO are both ready.'
    } else {
        Write-Host 'States      CO only. You cannot run the DC configuration.'
        Write-Host '            The DC connector is not at'
        Write-Host "            $DcConnector."
        Write-Host '            Clone it beside the portal, or point DC_CONNECTOR_PATH at your'
        Write-Host '            checkout, then run this script again.'
    }
    Write-Host ''

    Write-Host 'Next'
    Write-Host "  cd $Portal"
    Write-Host ''
    Write-Host '  Running the app needs one more choice, and README.md covers both:'
    Write-Host '    "Start services" and "Build and run the app" for the Compose path.'
    Write-Host '    "Local development with Aspire" for the Aspire path, which has its own'
    Write-Host '    one-time steps, including a certificate trust prompt that needs a person.'
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
if (-not (Copy-Repository $PortalRepo $Portal 'The portal')) {
    Stop-WithError @"
Could not clone the portal from $PortalRepo.
Behind a TLS-inspecting proxy, re-run with -SystemCerts. For a private fork, re-run with -Ssh.
"@
}

# Every version below comes out of the checkout above, so nothing here runs before
# the clone.
Test-Node
Test-Pnpm
Test-Dotnet
Test-ContainerRuntime

Write-Step 'Cloning the DC connector'
# DC is the one state whose connector lives outside this repository, and a developer
# without access to it still has a working CO workspace. So a failure here is a
# warning and a line in the summary, and not the end of the run.
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

Write-Summary
