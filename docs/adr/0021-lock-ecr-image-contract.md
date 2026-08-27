# 21. Lock the ECR API image restore by building the contract from source

Date: 2026-08-26

## Status

Accepted. Amends the "Contract references" decision in [ADR 0017](0017-monorepo-consolidation.md).

## Context

DC-356 introduces NuGet lockfiles and locked-mode restore, so every environment gets identical, hash-verified package versions. Enforcing that across the build is straightforward except for one target: the production API image, which is what deploys to DC dev and CO.

Per ADR 0017, the isolated Docker build copies only the portal projects and restores the contract (`SEBT.Portal.StatesPlugins.Interfaces`) as a NuGet package. The committed lockfile records that contract as a project reference — a version-less `Project` entry — because that's how the solution build resolves it. The image restores it as a package instead, which produces a different lockfile for the same project. Locked mode can't check the image's restore against a lockfile written for the other mode. Left alone, the one thing we ship would restore its dependencies unlocked.

## Decision

- **Enforce locked mode in CI everywhere.** `Directory.Build.props` sets `RestoreLockedMode` when `$(CI) == 'true'`. GitHub Actions sets `CI=true`, so every CI restore fails on a missing or drifted lockfile — both `build-backend.sh` copies, implicit `build`/`publish`, connector publishes. Local dev leaves `CI` unset and stays unlocked, so you can still add a package and refresh the lockfile.

- **Build the contract from source in the API image.** The Dockerfile mirrors the repo layout instead of flattening it, so `Api.csproj`'s relative reference to the contract resolves and it restores as a project reference — the same way the solution build does, against the same committed lockfile. The image copies the contract project and the closure lockfiles, and passes `--build-arg CI=true` so locked mode applies inside the build. This amends ADR 0017: the image now copies the contract project on top of the portal projects. It doesn't copy the plugin implementations — CO and DC still ship as prebuilt DLLs — so the image still doesn't depend on connector code that changes often.

- **The contract still ships as a package.** It keeps packing to `nuget-store` for the external DC connector and other out-of-tree builds. Only the image's copy of it changes.

- **Carry a `win-x64` section in every lockfile.** The DC IIS bundle publishes the API for `win-x64` (`scripts/ci/publish-api.sh`), which restores the whole closure a second time under that RID. A lockfile written without a RID has no `net10.0/win-x64` section, so that publish fails locked mode with `NU1004`. Restoring with the RID writes the section; `RuntimeIdentifiers` in `Directory.Build.props` makes it legal for the ordinary RID-less restores. Both halves are required — with the section but without the property, every RID-less restore fails `NU1004` instead, because the lockfile advertises a RID the project doesn't declare. The property is repo-wide rather than on the API alone: a RID flows down through project references, so scoping it to today's closure would break the next project added to it.

- **Where we stop.** The portal API and the CO plugin ship locked, image included. The DC plugin ships on `packageSourceMapping`, `NuGetAudit`, and its pinned direct versions — not lockfiles. To lock it we'd have to commit a project-reference-mode lockfile in the external dc-connector repo that only the monorepo publish could check, which couples the two repos' versions. It's not worth it: DC's direct dependencies are already pinned and its transitive tree is small. The DC publish steps in `deploy-ecr.yaml` and `release-iis-dc.yaml` restore with `-p:RestoreLockedMode=false`.

## Consequences

- The shipping API image restores pinned, hash-verified versions, or the build fails. We confirmed this: a tampered lockfile fails the image build with `NU1403`.
- The image build now needs the in-repo contract source. That's a deliberate narrowing of ADR 0017's boundary, and only for the contract — not the plugin implementations.
- The DC plugin's transitive dependencies in the shipping image aren't lockfile-pinned. Mapping, audit, and pinned direct versions cover them. Revisit if the DC connector moves in-repo — then its lockfiles are local and lockable without coupling two repos.
- When a referenced project's dependency is bumped, the referencing projects' lockfiles go stale, and Dependabot doesn't refresh them ([dependabot-core#12318](https://github.com/dependabot/dependabot-core/issues/12318)). Refresh every lockfile with both sections in one command:

  ```bash
  dotnet restore SEBT.slnx --runtime win-x64 --force-evaluate
  ```

  Passing the RID is what *creates* the `win-x64` sections. A plain `--force-evaluate` preserves sections that already exist but won't add them to a lockfile that lacks one — so a lockfile regenerated from scratch without the RID silently comes back missing it, and the IIS publish then fails `NU1004`. Always pass the RID.

## References

- `Directory.Build.props` — the `RestoreLockedMode` gate and the `RuntimeIdentifiers` declaration.
- `scripts/ci/publish-api.sh` — the `win-x64` API publish the RID section exists for.
- `apps/portal/src/SEBT.Portal.Api/Dockerfile` — mirrored layout, contract source, closure lockfiles, `ARG CI`.
- `.github/workflows/deploy-ecr.yaml`, `.github/workflows/release-iis-dc.yaml` — `--build-arg CI=true`; the DC-publish exemption.
- [ADR 0017](0017-monorepo-consolidation.md) — the amended contract-references decision.
