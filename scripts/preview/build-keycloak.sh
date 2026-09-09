#!/usr/bin/env bash
# Build and push the shared Keycloak image to ECR.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
AWS_REGION="${AWS_REGION:-us-east-1}"
IMAGE_TAG="${KEYCLOAK_IMAGE_TAG:-latest}"
ECR_REPOSITORY_URL="${ECR_KEYCLOAK_REPOSITORY_URL:-}"

if [ -z "${ECR_REPOSITORY_URL}" ]; then
  echo "ECR_KEYCLOAK_REPOSITORY_URL is required (tofu output keycloak_repository_url)." >&2
  exit 1
fi

echo "Building Keycloak image -> ${ECR_REPOSITORY_URL}:${IMAGE_TAG}"

aws ecr get-login-password --region "${AWS_REGION}" \
  | docker login --username AWS --password-stdin "${ECR_REPOSITORY_URL%%/*}"

docker build \
  --platform linux/amd64 \
  -t "${ECR_REPOSITORY_URL}:${IMAGE_TAG}" \
  -f "${ROOT_DIR}/docker/keycloak/Dockerfile" \
  "${ROOT_DIR}/docker/keycloak"

docker push "${ECR_REPOSITORY_URL}:${IMAGE_TAG}"
echo "Pushed ${ECR_REPOSITORY_URL}:${IMAGE_TAG}"
