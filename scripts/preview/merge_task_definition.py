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


def strip_secret_names(
    secrets: list[dict] | None,
    names_to_strip: set[str],
) -> list[dict]:
    """Remove secret entries by name so env overrides can replace them safely."""
    if not names_to_strip:
        return list(secrets or [])
    return [
        item
        for item in (secrets or [])
        if item.get("name") not in names_to_strip
    ]


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


def parse_args(argv: list[str]) -> tuple[dict[str, str], str, str, bool, str | None, set[str]]:
    if len(argv) < 4:
        raise ValueError(
            "Usage: merge_task_definition.py <env-overrides-json> <image> <family> "
            "[--strip-sidecars] [--strip-secret-names <json-array>] [container-name]"
        )

    overrides = json.loads(argv[1])
    image = argv[2]
    family = argv[3]
    remaining = argv[4:]

    strip_sidecars = False
    container_name: str | None = None
    strip_names: set[str] = set()

    i = 0
    while i < len(remaining):
        arg = remaining[i]
        if arg == "--strip-sidecars":
            strip_sidecars = True
            i += 1
        elif arg == "--strip-secret-names":
            if i + 1 >= len(remaining):
                raise ValueError("--strip-secret-names requires a JSON array argument")
            parsed = json.loads(remaining[i + 1])
            if not isinstance(parsed, list):
                raise ValueError("--strip-secret-names must be a JSON array of strings")
            strip_names = {str(name) for name in parsed}
            i += 2
        else:
            container_name = arg
            i += 1

    return overrides, image, family, strip_sidecars, container_name, strip_names


def main() -> int:
    try:
        overrides, image, family, strip_sidecars, container_name, strip_names = parse_args(sys.argv)
    except (ValueError, json.JSONDecodeError) as exc:
        print(str(exc), file=sys.stderr)
        return 1

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
        if strip_names:
            container["secrets"] = strip_secret_names(container.get("secrets"), strip_names)
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
        if strip_names:
            container["secrets"] = strip_secret_names(container.get("secrets"), strip_names)

    json.dump(cleaned, sys.stdout)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
