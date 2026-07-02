#!/usr/bin/env python3
"""Prepare an ECS task definition JSON for register-task-definition from a described task."""

from __future__ import annotations

import json
import sys

READ_ONLY_FIELDS = {
    "taskDefinitionArn",
    "revision",
    "status",
    "requiresAttributes",
    "compatibilities",
    "registeredAt",
    "registeredBy",
    "deregisteredAt",
}

AUXILIARY_CONTAINER_MARKERS = (
    "appconfig",
    "aws-otel",
    "otel",
    "adot",
    "fluent",
    "log_router",
)

APPCONFIG_ENV_PREFIX = "AppConfig__"


def merge_environment(
    existing: list[dict[str, str]] | None,
    overrides: dict[str, str],
) -> list[dict[str, str]]:
    merged: dict[str, str] = {}
    for item in existing or []:
        name = item.get("name")
        value = item.get("value")
        if name and value is not None:
            merged[name] = value
    merged.update(overrides)
    return [{"name": key, "value": value} for key, value in sorted(merged.items())]


def is_auxiliary_container(name: str) -> bool:
    lowered = name.lower()
    return any(marker in lowered for marker in AUXILIARY_CONTAINER_MARKERS)


def strip_appconfig_environment(
    environment: list[dict[str, str]] | None,
) -> list[dict[str, str]]:
    return [
        item
        for item in (environment or [])
        if not item.get("name", "").startswith(APPCONFIG_ENV_PREFIX)
    ]


def strip_auxiliary_containers(
    containers: list[dict],
    container_name: str | None,
) -> list[dict]:
    if container_name is not None:
        kept = [container for container in containers if container.get("name") == container_name]
    else:
        kept = [container for container in containers if not is_auxiliary_container(container.get("name", ""))]
        if not kept and containers:
            kept = [containers[0]]

    for container in kept:
        container.pop("dependsOn", None)
        container["environment"] = strip_appconfig_environment(container.get("environment"))

    return kept


def main() -> int:
    if len(sys.argv) < 4:
        print(
            "Usage: merge_task_definition.py <env-overrides-json> <image> <family> "
            "[--strip-sidecars] [container-name]",
            file=sys.stderr,
        )
        return 1

    overrides = json.loads(sys.argv[1])
    image = sys.argv[2]
    family = sys.argv[3]
    remaining_args = sys.argv[4:]

    strip_sidecars = False
    container_name: str | None = None
    for arg in remaining_args:
        if arg == "--strip-sidecars":
            strip_sidecars = True
        else:
            container_name = arg

    described = json.load(sys.stdin)
    task_definition = described.get("taskDefinition", described)

    cleaned = {
        key: value
        for key, value in task_definition.items()
        if key not in READ_ONLY_FIELDS
    }
    cleaned["family"] = family

    containers = cleaned.get("containerDefinitions", [])
    if strip_sidecars:
        # Sidecar containers are removed; task-level volumes from the base definition
        # are left intact and are usually harmless but can fail validation if AWS
        # or the base task definition changes.
        containers = strip_auxiliary_containers(containers, container_name)
        cleaned["containerDefinitions"] = containers

    updated = False
    for container in containers:
        if container_name is not None and container.get("name") != container_name:
            continue
        container["image"] = image
        container["environment"] = merge_environment(
            container.get("environment"),
            overrides,
        )
        updated = True
        if container_name is not None:
            break

    if not updated and containers:
        container = containers[0]
        container["image"] = image
        container["environment"] = merge_environment(
            container.get("environment"),
            overrides,
        )

    json.dump(cleaned, sys.stdout)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
