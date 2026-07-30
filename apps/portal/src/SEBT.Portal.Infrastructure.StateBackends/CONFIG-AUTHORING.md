# Authoring a state backend config

A state config bundle is a single YAML file that tells the portal how to talk to one state's household backend: its base URL, how to authenticate, which operations it supports, and how to translate each request and response between the portal's canonical vocabulary and the state's flavor. One rule governs everything in this guide: **you configure within a closed catalog of named bricks, and you never invent operators.** There is no expression language, no comparison syntax, no conditionals. When you need behavior no brick provides, you stop and ask for a new named brick to be added in code (see [When you hit the cap](#when-you-hit-the-cap)).

The two worked examples throughout are [`dc.sample.yaml`](../../test/SEBT.Portal.Tests/Unit/Infrastructure/StateBackends/ConfigSamples/dc.sample.yaml) (District of Columbia, API-key auth) and [`co.sample.yaml`](../../test/SEBT.Portal.Tests/Unit/Infrastructure/StateBackends/ConfigSamples/co.sample.yaml) (Colorado, OAuth client-credentials auth). Every YAML fragment below is copied from one of those files.

## Step 1: Base URL and authentication

Set `baseUrl` to the root of the state backend. Then declare an `auth` scheme. There are exactly two schemes.

**API key (DC).** A static key sent in a request header. `header` names the header; `keyRef` names the *configuration key* that holds the secret value.

```yaml
baseUrl: http://localhost:8085
auth:
  scheme: api_key
  header: X-Api-Key
  keyRef: dc-api-key
```

**Client credentials (CO).** An OAuth 2.0 client-credentials grant. `tokenUrl` is the token endpoint, `clientId` is the client identifier, and `clientSecretRef` names the *configuration key* that holds the client secret. An optional `scope` may be set.

```yaml
baseUrl: http://localhost:8086
auth:
  scheme: client_credentials
  tokenUrl: http://localhost:8086/oauth/token
  clientId: co-client
  clientSecretRef: co-client-secret
```

**Secrets are references, never values.** `keyRef` and `clientSecretRef` hold the *name* of a configuration key, not the secret itself. The runtime resolves that name against the environment / `/run/secrets`. Never paste an actual key or secret into the YAML.

## Step 2: Declare the operations

Everything the backend can do lives under `operations:`. The set of possible operations is fixed: `householdLookup`, `cardReplacement`, `addressUpdate`, `enrollmentCheck`, and `health`. **The presence of an operation is its capability** — if you declare `cardReplacement`, the portal reports that the state supports card replacement. If you omit it, the portal reports it as unsupported. The capability manifest is derived from the config; you never declare capabilities separately.

Every operation sets `method` and `path`, and optionally `timeoutSeconds`.

```yaml
operations:
  householdLookup:
    method: post
    path: /households/lookup
  health:
    method: get
    path: /health
```

`method` is one of `get`, `patch`, `post`, `put`. `path` is appended to `baseUrl`.

## Step 3: Map the response fields

Under a read operation's `response:`, `root` is a path to the record (or array of records) inside the raw response — dotted property access and `[index]` element access only. `fields` maps each of our canonical field names to how to pull it from that record.

```yaml
    response:
      root: $.resultSets[0]
      fields:
        summerEBTCaseID:
          from: SummerEBTCaseID
        childFirstName:
          from: ChildFirstName
        ebtCardIssueDate:
          from: EbtCardIssueDate
          format: yyyy-MM-ddTHH:mm:ss
        ebtCardStatus:
          from: EbtCardStatus
          enum: cardStatus
```

The mapping is **domain-centered**: the left-hand side is *our* canonical field name; the right-hand side (`from`) is the state's property name. A field mapping has three optional modifiers:

- `from` — the source property on the record. Required.
- `format` — an exact date parse format (e.g. `MM/dd/yyyy`) for date-typed fields. Exact parse, no fallback.
- `enum` — the name of an enum table (see Step 4) that translates the source token into a canonical value.

Coercion is driven by the canonical field's known type — a string field copies, a date field parses with `format`, an enum field resolves through its `enum` table. You do not declare the type.

## Step 4: Enum translation tables

Top-level `enums:` declares named translation tables that response fields reference by name via the field mapping's `enum` key. Each table is **domain-centered**: keyed by *our* canonical enum value, mapping to the one-or-more state tokens that mean it, plus a `default` for tokens the table does not list.

```yaml
enums:
  cardStatus:
    map:
      Active: [ACTIVE]
      Lost: [LOST, "LOST, AUTO REISSUE"]
    default: Unknown
```

```yaml
enums:
  applicationStatus:
    map:
      Approved: [AP]
      Denied: [DE]
      Pending: [PD, PE]
    default: Unknown
```

The table is inverted to a token→our-value lookup at load. Two things fail loud at load:

- A canonical key that is not a real member of the target enum.
- An ambiguous token — the same state token listed under two different canonical values.

The `default` applies **only** to genuinely unlisted tokens. A token mapped to a mistyped canonical value fails loud; it does not silently fall through to `default`.

## Step 5: keywordRules — inference from free text

When an enum-typed field can't be read from a single clean token but must be *inferred* from free text, use `keywordRules` instead of `enum`. It scans the field's `from` source(s) for keyword substrings. DC uses this to infer issuance type from household/eligibility free-text fields.

```yaml
        issuanceType:
          from: [HouseholdType, EligibilityType]
          keywordRules:
            order: [SummerEbt, SnapEbtCard, TanfEbtCard]  # first-match-wins — order is load-bearing
            map:
              SummerEbt:   [OSSE, NSLP]
              SnapEbtCard: [FOOD, SNAP]
              TanfEbtCard: [CASH, TANF]
            default: Unknown
```

Semantics: evaluate the canonical values in `order`; the first whose *any* substring (from its `map` entry) is contained in *any* of the `from` sources wins. Matching is case-insensitive. Nothing matches → `default`. **Ordering is load-bearing** — `order` decides which rule wins when more than one could match. A field using `keywordRules` may list several sources in `from`; a keyword found in any counts.

This brick is capped at substring-contains, first-match-wins. No regex, no conditionals, no transforms.

## Step 6: Disaggregation — records into cases and applications

A single flat backend response often mixes application-based and auto-issued records. `disaggregation` (under `response:`) declares how to group records into applications and which to include as cases, using a closed vocabulary — not an expression DSL.

DC — a record is application-based when its discriminator is *present*:

```yaml
      disaggregation:
        rule: presence
        discriminatorField: ApplicationId
        groupApplicationsBy: ApplicationId
        caseInclusion: all
```

CO — a record is application-based when its discriminator's value is *in a set*:

```yaml
      disaggregation:
        rule: valueInSet
        discriminatorField: eligSrc
        applicationValues: [CBMS, PK]
        groupApplicationsBy: sebtAppId
        caseInclusion: whenApprovedOrNotApplicationBased
```

- `rule` — `presence` (application-based when `discriminatorField` is non-empty) or `valueInSet` (application-based when its value is in `applicationValues`).
- `discriminatorField` — the source field the rule inspects.
- `applicationValues` — for `valueInSet`, the values that mean "application-based".
- `groupApplicationsBy` — the source field whose value groups records belonging to the same application.
- `caseInclusion` — a **named predicate** deciding which records become cases: `all` (every record) or `whenApprovedOrNotApplicationBased` (approved records, or records that aren't application-based).

`caseInclusion` names are a closed enum. A state that needs a new inclusion rule requires a new named predicate in code — you cannot express arbitrary logic here.

## Step 7: caseId — the opaque routing token

A write (card replacement, address update) has to route its call, but the portal must not understand state-specific routing identifiers. The `caseId` (under `response:`) is a self-describing, opaque token that solves this. Config only lists *which* record fields to pack into the token, keyed by *our* routing-field name; the encode/decode mechanism (pack fields → JSON → URL-safe base64) is fixed platform code.

```yaml
      caseId:
        fields:
          caseId: SummerEBTCaseID
          applicationId: ApplicationId
        fromContext:
          householdEmail: householdIdentifier
```

On a read, the mapper reads each named source field and packs it under its left-hand key into the token, which becomes the case's ID. On a later write, the driver decodes the token back into that same keyed field set and exposes those fields as inputs to the write's request binding (Step 8). The portal and UI treat the token as opaque throughout — a malformed token fails loud on decode.

- `fields` — token field → the response record property whose value it carries.
- `fromContext` — token field → a **named caller-context value** from the lookup itself, for routing identifiers a write needs but the response never echoes (most lookups don't echo the identifier the portal searched with). Context names are a **closed vocabulary resolved in fixed code** — today only `householdIdentifier`, the identifier value the lookup searched by. No expressions, no fallbacks; a new context value means a new name in code.

A token field may come from `fields` or `fromContext`, never both — the loader fails loud on a collision, and on an unknown context name. An unset context value packs as empty, exactly like an absent response column.

## Step 8: Writes — request binding and result classification

A write operation (`cardReplacement`, `addressUpdate`) has a `request:` binding that builds the outgoing body and a `result:` classifier that reads the outcome.

### Request binding

The binding vocabulary:

- `constants` — dotted target path → fixed literal (bool, number, string). State scaffolding with no domain source.
- `map` — our input name → dotted target path in the request body. Inputs are the decoded `caseId` routing fields plus caller context (e.g. the address scalars `line1`/`line2`/`city`/`state`/`zip`). Nesting is expressed by dotting the target path.

DC card replacement — a scalar `map` whose left-hand names are the decoded `caseId` fields:

```yaml
    request:
      map:
        caseId: summerEbtCaseId
        householdEmail: householdEmail
```

Address update spans every case a household owns, so it adds two **batch shapes**:

- `shared` — a household-level routing field resolved **once** across every decoded `caseId`. Left-hand side is a decoded routing-field name; right-hand side is a target path. The binder **fails loud if the decoded caseIds disagree** on the value. DC resolves one shared household identifier this way:

```yaml
    request:
      constants:
        source: portal
      shared:
        householdEmail: householdIdentifier
      map:
        line1: address.line1
        city: address.city
        state: address.state
        zip: address.zip
```

- `collect` — a per-case routing field gathered into an **array** at a target path, one element per decoded `caseId`. CO collects each case's per-case write-id into a PATCH array:

```yaml
    request:
      collect:
        writeId: cases
      map:
        line1: stdAddr
        zip: stdZip
```

`shared` and `collect` are the only two batch shapes. There are no per-case conditionals, filters, or transforms.

Note the read/write asymmetry: reads bind by walking the *response* shape (Step 3), while writes bind by building the *produced payload's* shape — the binding is keyed by the target path in the body you're constructing.

### Result classifier

`result:` classifies the backend's response into a canonical outcome. It's an **ordered, first-match-wins** list of `conditions` plus a `default`. Each condition maps to exactly one `outcome` — one of the `WriteOutcome` members `Success`, `PolicyRejection`, `BackendError` — and is **exactly one** of three closed kinds:

- `statusIn: [ints]` — the HTTP status code is in this set.
- `valueIn: [strings]` + `field` — the response body property `field`'s value is in this set.
- `messageContains: [strings]` + `messageField` — the body property `messageField` contains any of these substrings (case-insensitive).

DC — order matters: a "policy" message is checked *before* success, so a policy rejection isn't misread as success:

```yaml
    result:
      conditions:
        - outcome: policyRejection
          messageField: resultMessage
          messageContains: [policy]
        - outcome: success
          field: resultCode
          valueIn: ["0"]
      default: backendError
```

CO:

```yaml
    result:
      conditions:
        - outcome: success
          field: respCd
          valueIn: ["200", "00"]
      default: backendError
```

`default` applies when no condition matches; it defaults to `BackendError` when unset. No AND/OR combinators, no nesting — if a real case needs to combine conditions, stop.

## Step 9: Enrollment check

The enrollment op turns a batch of children into backend calls, then decides a match per child. Two axes: how the batch fans out (`callMode`), and how a match is decided (`match.strategy`).

**`callMode`** is required — the call shape is never inferred.

- `batch` — one backend call carries every child as a correlated row. Requires an `indexField` on **both** the request binding and response mapping so rows correlate back to children. Supports candidate `expand`.
- `perChild` — the driver loops the batch and makes one call per child, reading a single result object each. Must **not** set an `indexField` on either side (there's nothing to correlate), and does not support `expand` yet.

**`expand`** (request side) is a closed candidate-expansion brick, not a date-mangling language. `transposeMonthDay` emits the entered DOB plus its month/day-swapped candidate — but only when the swap yields a valid *and* different date — under the same correlation index. Omit it (or set `none`) for exactly one row per child.

**`match.strategy`** is one of two named strategies:

- `anyRowValueIn` — needs `field` + `valueIn`. A row matches when `field`'s value is in `valueIn`. In batch mode a child matches if *any* of its rows match.
- `confidenceThreshold` — needs `scoreField` + `threshold`. A child matches when its best candidate's score is *strictly greater than* `threshold`. The argmax and the `>` live in code; config only supplies the field and number.

DC — `perChild`, single result object as root, `anyRowValueIn`:

```yaml
  enrollmentCheck:
    method: post
    path: /enrollment/check
    callMode: perChild
    request:
      map:
        firstName: firstName
        lastName: lastName
        dob: dateOfBirth
    response:
      root: $
      match:
        strategy: anyRowValueIn
        field: isEligible
        valueIn: ["true"]
```

CO — `batch`, correlated rows via `indexField`, DOB `expand`, `confidenceThreshold`:

```yaml
  enrollmentCheck:
    method: post
    path: /sebt/check-enrollment
    callMode: batch
    request:
      expand: transposeMonthDay
      indexField: stdReqInd
      map:
        firstName: stdFirstName
        lastName: stdLastName
        dob: stdDob
    response:
      root: $.stdntDtls
      indexField: stdReqInd
      match:
        strategy: confidenceThreshold
        scoreField: mtchCnfd
        threshold: 90
```

The request `map`'s left-hand keys are the fixed child fields `firstName` / `lastName` / `dob`.

## When you hit the cap

The brick catalog is deliberately finite. When a state needs something no brick expresses — a new match rule, a new inclusion predicate, a new expansion — **stop.** Do not add operators, conditionals, or an expression syntax to the YAML. Doing so turns config into a programming language no one can audit.

Instead, add a **new named brick**: a new enum member plus its fixed algorithm in code, with tests. Or ask, if you're not sure it belongs in the platform.

The worked precedent is `confidenceThreshold`. CO needed a match that couldn't be expressed as "value in a set." The answer was not a numeric-comparison DSL in YAML. It was a new *named strategy* — `confidenceThreshold` — whose argmax and strict `>` live in code, with config supplying only the score field and threshold. That's the pattern every future need follows.

## How config is validated

Config validates at **load** via `StateBackendConfigurationValidator`, immediately after deserialization. Every check is a function of the config alone, so a bad config fails at **startup**, not on the first user request. What fails loud:

- An enum table whose canonical key isn't a real enum member, or an ambiguous state token listed under two canonical values.
- A `keywordRules` block whose `order` doesn't cover every `map` key or names a non-member.
- A result classifier condition that isn't exactly one of `statusIn` / `valueIn` / `messageContains`, or a `valueIn` without `field`, or a `messageContains` without `messageField`.
- An incoherent enrollment op: `batch` missing an `indexField` on either side, `perChild` that sets one, `perChild` combined with `expand`, or a match strategy missing its required params (`anyRowValueIn` without `field` + `valueIn`, `confidenceThreshold` without `scoreField` + `threshold`).
