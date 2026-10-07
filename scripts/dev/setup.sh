#!/bin/bash
print_potato() {
  cat <<'SEBT_POTATO'

                  ++++****++--===*                                                                   
         ##++*+ =*+=+++++*=....:=*=           +*+*+                                                  
        %*=====**+++++++++....-+++*        *#*++++=*#***#%#+                             ==          
     #####+=+==+*#**#****-==:--===***+   *#*++++++**########*                     =+**+=:.:*+        
    ##++++#+===+=+*=****#:...:+***+==#+ *#*++++*##*+#######%+                =+**=::..:=+**#*        
    %*++***#++**+==+*%#%##*-+*###+*+=*  ##**+*#*++#*+===--+#*+          =+**=....-+******+-:+        
       +*+++****+++***%%%%#*#*###*#**   *####****+==---------**=        #-.-=+*****+=-::--..-*       
       ***+++*#++++*#%%###%%#%#**+==**  =##+=------------=----=#+       +#****=:..:=+=--+....*+      
         ##**+****##**+=====+++++**##*  **+=-==----------------=#+       *+...=*+:=++*:.==....*=     
         *#+=====+++************++*#+  +#+=------==------=**+=--+#       *#==--*+-.+=+=..:.:=+*=     
          ####******++++*+++=++****#*  #*+=---=#+=---------------*#    +*-.:+-.+*+-...:+**+#         
           +*=++*+++++***+++*==++**** +#++=------++=-----=%+=+---+*    *+:.:+:..:-+#*=               
            #**++===+*+==+++******##  *#++=-----*%=#-----=@@@#----*+   +*=.:++++**                   
             #**+*****++**+=++***##+  ##++=-----+@@%------=#*-----=#=  *+:.:-=**                     
              **++***=+*::***++*+     **+==-=---------------=------+*  +*##+**                       
               +*%%##+=:=:......+=    #*+==-------=##*+*##%@#-------*####*+                          
                       +*####--*#+   +**++=--------%@@@%@@@@=-------=*+-                             
      =****++                  **########+=---------=#***#*----------*#                              
      *#########******+++**==     +*##*+===---------------------==---*+       =*#%%%#######%%%*+=    
      *######################*       +*+++==--==---------------------*+#+*%%#+-*#****+======+***%#   
     +*######################+       *#*+++=--===--------------------**+-::::::=#+==++*********#%*   
     *-...:####**############*        **==+==-------------==--------=+-::::::::-*#************###    
    =+....*##=----++:.......-*        +#+=====---------------------=*=::::::::::-#***********####    
    *=....###-----:..:-.....-+         +#+======----=----------=-==*+-:-+***+-:::-%#*******###%#     
    *####*####*==*#+=.... ..==          *#*===++===-----------==++#+-=##**+**##-::-##*****###%#      
   +*###########*==#######*+*            =*#++========++==+++==+**=-=#*+-:..:+*#-::-*#**####%+       
   +*#########*==*##########*              =***+=====+###*+=+**+=-::+#*------=*#=::::=#####%#        
    **++**#**--+############*        *#%%%%%%%*+%##########*===-::::=%*=-----+*#-::-===+#%#          
         +*=-=*+=*=+*****#***    +##*+=+****++##=========+##+-::::::-+##**+**##=:-=====*#=           
       **+:-=**               *##*+**#########======-----=*#+:::::::::-+*###*=--=====+%#*            
      +*=:-=*+              +%#+*##########%=:::::::::::-=##=:::::::::::::::-======+%*               
    +**-:-=**+            *#%###%%##%%####*-::::::::::::-+#####=::::::::--=======*##=                
   ***-:-=**+                        ++#%=::::::-+****-:==+**###+::::--=======+##*                   
  =**-:-=***                         +%+**:::=%#==+*%*:::::--===-:-=========#%*-  +**                
  **=:--+**                    -=====****#*#*=+***##-::::::::---========+###+  *+=..:++              
  *+::-=**+                -=+++++===+####++***#%*=:::::---=========+*###*    +=......:++            
   :.--=**+               =++++---:::-*#+**#%%+==---============+#%#*       ++.... .....-+           
  =.:--=****          ++++++++=-::::*%##%%%#%#===========++*##%#+=         +:..::...:....-=++        
  =.:---+****=   =+*******++++-::::-+=--=%#####+++**##%%###*               +=..-:...-:..:=:.==       
  =:.:---=************+==+++=:::::::::--+=#%%#*%########*#*                 +***##############*+     
  *::.:-----=++++==----++++=:::-------=++-    #%#######*##*                    **+++++++++++++***    
   =:..::-------------++++++++++++==+++==    =#######**%*                       ++**########***      
    =-::...:::::::::-++++++++++*+++++=-      *%####**##                           =*++++++**+        
     *=-:.:::::....::::::-=+***+++=-        *%######*                             ++:....=*          
         -==--:::--=++*******+++          +###%%**                                +=...-+            
               **********+=              *#%*+                                    ==.-++             
                 *+=                                                              =+++                                                                                                                                            
SEBT_POTATO
}

# First-time local setup: copy example configs, install deps, build, start Docker.
# Does not overwrite existing config files. Does not start the app.
#
# Usage:
#   ./scripts/dev/setup.sh [--state co|dc|both] [--with-redis] [--no-art]
# Omitting state or Redis in a terminal prompts for it.

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
MULTI_PROJECT_ROOT="$(cd "$PROJECT_ROOT/.." && pwd)"
DC_CONNECTOR="$MULTI_PROJECT_ROOT/sebt-self-service-portal-dc-connector"
DC_CONNECTOR_REMOTE="https://github.com/codeforamerica/sebt-self-service-portal-dc-connector.git"
API_DIR="$PROJECT_ROOT/apps/portal/src/SEBT.Portal.Api"
WEB_DIR="$PROJECT_ROOT/apps/portal/src/SEBT.Portal.Web"

STATE=""
WITH_REDIS=0
REDIS_SET=0

log_info() {
  printf '%b\n' "${BLUE}ℹ️  $1${NC}"
}

log_success() {
  printf '%b\n' "${GREEN}✅ $1${NC}"
}

log_warning() {
  printf '%b\n' "${YELLOW}⚠️  $1${NC}"
}

log_error() {
  printf '%b\n' "${RED}❌ $1${NC}" >&2
}

usage() {
  cat <<'EOF'
First-time local setup for the SEBT portal.

Usage:
  ./scripts/dev/setup.sh [--state co|dc|both] [--with-redis] [--no-art]

Options:
  --state co|dc|both   Which state to prepare.
                       dc and both clone the sibling DC connector repo if needed.
                       If omitted in a terminal, the script asks.
  --with-redis         Generate local Redis TLS certs and start Redis.
                       If omitted in a terminal, the script asks.
  --no-art             Do not print the potato banner.
  -h, --help           Show this help.

The script copies example configs when the destination is missing, installs
JS and .NET dependencies, builds, and starts Docker services. After it finishes,
run pnpm dev:co or pnpm dev:dc.
EOF
}

copy_if_missing() {
  local src="$1"
  local dest="$2"
  local label="${dest#"$PROJECT_ROOT"/}"

  if [ -e "$dest" ]; then
    log_info "Keeping existing $label"
    return 0
  fi
  if [ ! -f "$src" ]; then
    log_error "Missing example file: $src"
    return 1
  fi
  cp "$src" "$dest"
  log_success "Created $label"
}

compose_services() {
  local state="$1"
  local with_redis="$2"
  local services="mssql"

  case "$state" in
    dc|both) services="$services mailpit" ;;
  esac
  if [ "$with_redis" = "1" ]; then
    services="$services redis"
  fi
  echo "$services"
}

prompt_state() {
  local choice
  echo ""
  echo "Which state should this machine be set up for?"
  echo "  1) co"
  echo "  2) dc"
  echo "  3) both"
  while true; do
    read -r -p "Choice [1]: " choice
    case "${choice:-1}" in
      1|co|CO) STATE="co"; return ;;
      2|dc|DC) STATE="dc"; return ;;
      3|both|BOTH) STATE="both"; return ;;
      *) log_error "Enter 1, 2, 3, co, dc, or both" ;;
    esac
  done
}

prompt_redis() {
  local choice
  while true; do
    read -r -p "Start Redis with local TLS certs? [y/N] " choice
    case "$(printf '%s' "$choice" | tr '[:upper:]' '[:lower:]')" in
      y|yes) WITH_REDIS=1; return ;;
      n|no|"") WITH_REDIS=0; return ;;
      *) log_error "Enter y or n" ;;
    esac
  done
}

parse_args() {
  while [ $# -gt 0 ]; do
    case "$1" in
      --state)
        if [ $# -lt 2 ]; then
          log_error "--state requires a value (co, dc, or both)"
          usage
          exit 1
        fi
        STATE="$2"
        shift 2
        ;;
      --state=*)
        STATE="${1#--state=}"
        shift
        ;;
      --with-redis)
        WITH_REDIS=1
        REDIS_SET=1
        shift
        ;;
      --no-art)
        shift
        ;;
      -h|--help)
        usage
        exit 0
        ;;
      *)
        log_error "Unknown option: $1"
        usage
        exit 1
        ;;
    esac
  done

  case "$STATE" in
    co|dc|both) ;;
    "")
      if [ -t 0 ]; then
        prompt_state
      else
        log_error "--state is required (co, dc, or both)"
        usage
        exit 1
      fi
      ;;
    *)
      log_error "Invalid --state '$STATE' (use co, dc, or both)"
      usage
      exit 1
      ;;
  esac

  if [ "$REDIS_SET" != "1" ] && [ -t 0 ]; then
    prompt_redis
  fi
}

require_command() {
  local name="$1"
  local hint="$2"
  if ! command -v "$name" >/dev/null 2>&1; then
    log_error "$name is not installed. $hint"
    exit 1
  fi
}

major_version() {
  echo "$1" | sed 's/^v//' | cut -d. -f1
}

check_prerequisites() {
  log_info "Checking prerequisites..."

  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
      log_warning "Windows detected. Enable long paths: git config core.longpaths true"
      ;;
  esac

  require_command git "Install from https://git-scm.com/install/"
  require_command dotnet "Install the .NET 10 SDK from https://dotnet.microsoft.com/download (or: brew install dotnet)"
  require_command node "Install Node.js 24 from https://nodejs.org/ (or: brew install node)"
  require_command pnpm "Install from https://pnpm.io/installation/ (or: brew install pnpm)"
  require_command docker "Install Docker Desktop from https://www.docker.com/ and start it"

  if ! dotnet --list-sdks | grep -q '^10\.'; then
    log_error ".NET 10 SDK is required. Install from https://dotnet.microsoft.com/download"
    exit 1
  fi
  log_success ".NET SDK $(dotnet --version)"

  local node_major
  node_major="$(major_version "$(node -v)")"
  if [ "$node_major" -lt 24 ]; then
    log_error "Node.js 24 or later is required (found $(node -v))"
    exit 1
  fi
  log_success "Node.js $(node -v)"

  local pnpm_major
  pnpm_major="$(major_version "$(pnpm -v)")"
  if [ "$pnpm_major" -lt 10 ]; then
    log_error "pnpm 10 or later is required (found $(pnpm -v))"
    exit 1
  fi
  log_success "pnpm $(pnpm -v)"

  if ! docker compose version >/dev/null 2>&1; then
    log_error "docker compose is not available. Install Docker Desktop and start it."
    exit 1
  fi
  if ! docker info >/dev/null 2>&1; then
    log_error "Docker daemon is not running. Start Docker Desktop and retry."
    exit 1
  fi
  log_success "Docker is running"
}

copy_configs() {
  log_info "Copying example configs (existing files are left alone)..."
  copy_if_missing "$PROJECT_ROOT/.env.example" "$PROJECT_ROOT/.env"
  copy_if_missing "$WEB_DIR/.env.example" "$WEB_DIR/.env.local"
  copy_if_missing "$API_DIR/appsettings.Development.example.json" "$API_DIR/appsettings.Development.json"

  if [ "$STATE" = "co" ] || [ "$STATE" = "both" ]; then
    copy_if_missing "$API_DIR/appsettings.co.example.json" "$API_DIR/appsettings.co.json"
  fi
  if [ "$STATE" = "dc" ] || [ "$STATE" = "both" ]; then
    copy_if_missing "$API_DIR/appsettings.dc.example.json" "$API_DIR/appsettings.dc.json"
  fi
}

ensure_dc_connector() {
  if [ -d "$DC_CONNECTOR/.git" ]; then
    log_success "DC connector already cloned"
    return 0
  fi

  log_info "Cloning DC connector next to this repo..."
  if git clone "$DC_CONNECTOR_REMOTE" "$DC_CONNECTOR"; then
    log_success "Cloned DC connector"
    return 0
  fi

  log_error "Could not clone the DC connector. Check GitHub access, or clone it manually into:"
  log_info "  $DC_CONNECTOR"
  exit 1
}

install_and_build() {
  cd "$PROJECT_ROOT"

  log_info "Installing JavaScript dependencies..."
  pnpm install

  log_info "Restoring .NET tools..."
  dotnet tool restore

  case "$STATE" in
    dc|both)
      log_info "Building portal and DC connector..."
      "$PROJECT_ROOT/scripts/dev/build-dc.sh"
      ;;
    *)
      log_info "Building portal and in-repo connectors..."
      dotnet build SEBT.slnx
      ;;
  esac
}

start_docker_services() {
  cd "$PROJECT_ROOT"

  if [ "$WITH_REDIS" = "1" ]; then
    log_info "Generating Redis TLS certs..."
    "$PROJECT_ROOT/scripts/dev/gen-redis-certs.sh"
  fi

  local services
  services="$(compose_services "$STATE" "$WITH_REDIS")"
  log_info "Starting Docker services: $services"
  # shellcheck disable=SC2086
  docker compose up -d --wait $services
  log_success "Docker services are up"
}

print_next_steps() {
  echo ""
  log_success "Setup complete. The app is not running yet."
  echo ""
  echo "Start the app:"
  case "$STATE" in
    co)
      echo "  pnpm dev:co         # Colorado portal"
      echo "  pnpm dev:co-enroll  # Colorado enrollment checker"
      ;;
    dc)
      echo "  pnpm dev:dc         # DC portal"
      ;;
    both)
      echo "  pnpm dev:co         # Colorado portal"
      echo "  pnpm dev:dc         # DC portal"
      echo "  pnpm dev:co-enroll  # Colorado enrollment checker"
      ;;
  esac
  echo ""
  echo "Then open https://localhost:3000"
  case "$STATE" in
    dc|both)
      echo "Mailpit (DC OTP emails): http://localhost:8025"
      ;;
  esac
  if [ "$WITH_REDIS" = "1" ]; then
    echo "Redis TLS is on localhost:6380. Example appsettings already include a local Redis block."
  fi
}

main() {
  local show_art=1
  local arg
  for arg in "$@"; do
    if [ "$arg" = "--no-art" ]; then
      show_art=0
    fi
  done
  if [ "$show_art" = "1" ]; then
    print_potato
  fi
  parse_args "$@"

  log_info "=== SEBT first-time setup ==="
  log_info "Project: $PROJECT_ROOT"
  log_info "State: $STATE"
  echo ""

  check_prerequisites
  copy_configs
  case "$STATE" in
    dc|both) ensure_dc_connector ;;
  esac  
  install_and_build
  start_docker_services
  print_next_steps
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  main "$@"
fi
