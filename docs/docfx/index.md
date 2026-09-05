---
_layout: landing
description: Documentation for the Summer EBT (SUN Bucks) Self-Service Portal and Enrollment Checker. Extensible, multi-platform, and multilingual by design.
keywords: summer EBT SUN Bucks portal enrollment checker documentation onboarding setup state connector extensibility plugin multiplatform multilingual translation compliance security features
---

# Summer EBT Self-Service Portal

Give families a way to manage their Summer EBT benefits, in their own language, on the platform your state already
runs. In use today by Colorado and Washington, DC.

<ul class="intro-capabilities">
  <li><span class="bi bi-search" aria-hidden="true"></span>Check enrollment, no account</li>
  <li><span class="bi bi-house-heart" aria-hidden="true"></span>See benefits and card status</li>
  <li><span class="bi bi-credit-card-2-front" aria-hidden="true"></span>Activate or replace a card</li>
  <li><span class="bi bi-geo-alt" aria-hidden="true"></span>Update an address</li>
</ul>

<div class="cta-grid">
  <a class="cta" href="https://github.com/codeforamerica/sebt-self-service-portal#local-environment-set-up">
    <span class="cta-icon bi bi-terminal" aria-hidden="true"></span>
    <span class="cta-title">Set up your environment</span>
    <span class="cta-text">Install, run, and test the portal locally.</span>
  </a>
  <a class="cta" href="guides/state-connector/index.md">
    <span class="cta-icon bi bi-puzzle" aria-hidden="true"></span>
    <span class="cta-title">Add a new state</span>
    <span class="cta-text">Build a connector to your state's systems of record.</span>
  </a>
  <a class="cta" href="guides/content/index.md">
    <span class="cta-icon bi bi-translate" aria-hidden="true"></span>
    <span class="cta-title">Change what families see</span>
    <span class="cta-text">Update wording in any supported language.</span>
  </a>
  <a class="cta" href="compliance/index.md">
    <span class="cta-icon bi bi-shield-check" aria-hidden="true"></span>
    <span class="cta-title">Review compliance</span>
    <span class="cta-text">Licensing, accessibility, security, and identity proofing.</span>
  </a>
</div>

## How the pieces fit

```mermaid
flowchart TB
  subgraph apps ["Web apps"]
    direction LR
    W["Portal"]
    E["Enrollment Checker"]
  end

  API["Portal API"]

  subgraph conn ["State connectors"]
    direction LR
    CO["Colorado"]
    DC["Washington, DC"]
    NEW["Your state"]
  end

  SOR[("State systems of record")]

  W --> API
  E --> API
  API --> CO
  API --> DC
  API --> NEW
  CO --> SOR
  DC --> SOR
  NEW --> SOR
```

Everything state-specific lives in the bottom lane. The API above it knows only the interfaces, so a new state is a
new connector rather than a change to the portal.

## More

- [Architecture Decisions](adr/index.md) explain why the system is shaped the way it is.
- [.NET API Reference](api/index.md) covers every published C# type.
- [Releases](releases.md) tell you what changed and when.
- [Repository README](https://github.com/codeforamerica/sebt-self-service-portal#readme) holds installation, local
  development, and database setup.
