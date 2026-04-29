---
name: refactoring
description: Use before restructuring code, migrating between models, renaming abstractions, moving behavior between layers, extracting methods/classes, consolidating duplication, or any cleanup that reshapes structure without changing behavior. Apply this skill even when the user says "clean up," "reorganize," "extract," "move," "simplify," or "tidy" rather than "refactor" — the intent (change shape, preserve behavior) is what matters. Engage the skill before making changes, not after.
---

# Refactoring

Refactoring is changing the structure of code without changing its observable behavior. Tests are the proof that behavior is preserved.

## The Rule

**If existing tests must be deleted or weakened for a refactor to succeed, you are not refactoring — you are changing behavior.** Stop and treat it as a separate, deliberate decision (see "Separating Structure Changes from Behavior Changes" below).

## Scope discipline

Refactor only what the current task requires. Two failure modes to avoid:

- **Drift** — you start renaming a type and end up restructuring a layer. Each additional change widens the diff, weakens reviewability, and increases the chance of a behavioral regression slipping through.
- **The "I notice this is messy" trap** — mid-refactor, you spot adjacent code that could be cleaner. Resist. Document it as a follow-up (a TODO, a note, an issue) and stay on the original task. Future-you can pick it up with a clean head; current-you is already holding too much state.

If the user's request is ambiguous about scope, ask before expanding it.

## Process

1. **Identify the behavioral contract** — count tests covering the code, list behaviors they verify, note the assertions. These are the contract.
2. **If coverage gaps exist**, write characterization tests first (see "Behavioral Contract" below).
3. **Run all tests — they must be GREEN before you touch anything.** If they're already failing, fix that first or stop; you can't tell what your refactor broke if the baseline is red.
4. **Make the structural change.**
5. **Re-run all tests.** Three outcomes:
   - **GREEN** → done.
   - **Compilation errors only** → update test signatures (types, imports, renames, fixture construction). Assertions must not change in meaning. Re-run.
   - **Logic errors (assertions failing)** → you broke behavior. Fix the code, not the tests. If you find yourself wanting to change an assertion to pass, STOP — see "Separating Structure Changes from Behavior Changes."

## Behavioral Contract

Before touching code, identify what the existing tests prove:

1. **Count the tests** that cover the code you're changing
2. **List the behaviors** they verify (validation, enforcement, error paths, happy paths)
3. **Note the assertions** — these are the contract

If coverage is thin or uncertain, write **characterization tests** first. A characterization test asserts _what the code currently does_, not what it _should_ do — it captures existing behavior as a baseline, even if you don't fully understand it. These become your safety net during the refactor.

Note: the behavioral contract includes both tested and untested behavior. If you know the code does something that no test covers, write a characterization test for it before proceeding.

## Safe Refactorings Without Tests

Some mechanical transformations are safe to perform even without test coverage — and are often necessary _in order to create_ test coverage before larger refactoring steps. These include extract method, rename, introduce parameter, and others.

See [safe-refactorings.md](safe-refactorings.md) for the full list with guidance on what to watch for.

## What You May Change in Tests

During a refactor, tests often need updates to compile against new types or signatures. This is expected. The constraint:

| Allowed                                                 | Not Allowed                   |
| ------------------------------------------------------- | ----------------------------- |
| Rename a type everywhere (`AppRequest` → `CaseRequest`) | Delete a test                 |
| Update property names (`appNumber` → `caseId`)          | Weaken an assertion           |
| Change import paths                                     | Remove a scenario             |
| Update fixture construction                             | Reduce the number of tests    |
| Rename test methods to match new terminology            | Change what a test _verifies_ |

**The assertion count and the scenarios they cover must be equivalent before and after.** Assertions may change _syntactically_ (new property names) but not _semantically_ (what they verify).

## Yellow-Light Changes — Ask Before Proceeding

Some changes look structural but carry behavioral risk. These require human confirmation before proceeding.

### Swapping one type for another

Renaming a type everywhere (e.g., in this project, `AppRequest` → `CaseRequest` with identical fields) is safe — it's the same type with a new name. A rename is safe only if the type's shape (fields, methods, invariants) is unchanged.

**Replacing one type with a different type** (e.g., changing a function from accepting `Application` to accepting `SummerEbtCase`) is a behavioral change in disguise. The two types have different fields, different semantics, and different invariants. Even if the code compiles, the behavior may have changed. If the type's shape changes alongside a rename, treat it as a type swap, not a rename.

**Before swapping types:** Confirm with the human what behavioral differences are acceptable. Map the fields from the old type to the new type explicitly — any field that doesn't have a direct equivalent is a behavioral gap.

### Moving fields or behavior between models

Migrating a field from Model A to Model B (e.g., in this project, moving `CardRequestedAt` from `Application` to `SummerEbtCase`) changes _where_ behavior is sourced. The behavior should transfer wholesale:

- Every place that read the field on Model A must now read it on Model B
- Every enforcement, validation, or logic that depended on the field must be preserved
- Tests that verified the behavior must continue to verify it on the new model

**Before moving fields:** Confirm with the human the scope of acceptable change. Present the field mapping and ask: "These behaviors currently depend on this field on Model A — I'll preserve all of them on Model B. Are there any that should intentionally change?"

### Adding a field to a model to preserve behavior

When migrating behavior from Model A to Model B and Model B lacks a field that Model A had:

**Default to adding the field to Model B.** A missing field is usually a signal that the target model is incomplete — not that the behavior is unnecessary.

If you believe the behavior was a workaround or technical debt that should not transfer, that's a behavioral change — separate it into its own commit and confirm with the human. Don't silently drop it during a structural migration.

## Separating Structure Changes from Behavior Changes

If a refactor genuinely needs to change behavior (remove a feature, relax a constraint), do it in two steps:

1. **First commit: structural refactor** — all existing tests pass, behavior preserved
2. **Second commit: behavioral change** — tests updated deliberately, reviewed separately

This makes the behavioral change visible and reviewable on its own, rather than hidden inside a structural change. If the process above leads you to STOP, this is your recovery path.

## Red Flags — STOP and Reassess

- Deleting tests to make a refactor pass
- Replacing enforcement logic with a TODO
- Weakening assertions ("was `Assert.Equal`, now `Assert.NotNull`")
- Test count decreased after refactor
- A subagent reports "tests removed because the feature no longer applies" during what should be a structural migration
- The phrase "this behavior no longer exists" when you only intended to change structure

**All of these mean: the refactor is changing behavior. Separate the structural change from the behavioral change.**
