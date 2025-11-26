#!/bin/bash
# Docker wrapper to capture ACT output in container logs
# This makes workflow output visible in Docker Desktop

# Run ACT and pipe to both stdout and container logs
exec 1> >(tee /dev/stdout | logger -t act-workflow)
exec 2> >(tee /dev/stderr | logger -t act-workflow)

# Execute the actual workflow command
exec "$@"
