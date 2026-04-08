# Safe Refactorings Without Test Coverage

These mechanical transformations are safe to perform even without existing test coverage. They are often necessary prerequisites — you may need to apply them *in order to* create test coverage before doing larger refactoring work.

Each preserves behavior by definition. "Safe" means behaviorally safe — not necessarily performance-neutral. If you find yourself making a judgment call about whether behavior changes, the refactoring is not on this list — stop and get test coverage first.

## Extract Method

Pull a block of code into a named method. The original site calls the new method with the same arguments.

**Safe because:** Same code runs in the same order. Only the call site changes.

**Watch for:** Extracted method captures mutable state by reference. If the block modifies local variables used later, pass them as parameters and return the modified values.

## Extract Variable / Introduce Explaining Variable

Replace a complex expression with a named local variable.

**Safe because:** Same expression evaluates once either way. The variable just gives it a name.

## Rename (method, variable, class, file)

Change the name of a symbol everywhere it appears.

**Safe because:** Names don't affect behavior. Use grep to find all occurrences and rename systematically.

**Watch for:** Strings that reference the old name (serialization, reflection, API contracts, database columns). These are behavioral — renaming the symbol without updating the string changes behavior.

## Inline Method / Inline Variable

Replace a method call with its body, or a variable with its expression. The inverse of Extract.

**Safe because:** Same code runs either way.

**Watch for:** Method is overridden in a subclass — inlining skips the override.

## Move Method / Move Field

Move a method or field from one class to another where it better belongs.

**Safe because:** The code and its callers still connect the same way. Only the location changes.

**Watch for:** This is safe primarily for static methods and pure functions. For instance methods that reference `this` (instance state), moving to another class means it no longer has implicit access to the original class's fields — you must pass them explicitly, and missing a dependency changes behavior silently. Also watch for access modifiers — moving a private method to another class may require making it public, which widens its visibility.

## Extract Class / Extract Module

Pull a coherent group of fields and methods out of a large class into a new class. The original class delegates to the new one.

**Safe because:** Same code runs, just organized into two classes instead of one. Callers of the original class are unchanged.

**Watch for:** Shared mutable state. If the extracted fields are modified by methods that stay in the original class, you need to ensure both classes reference the same state (typically by having the original class hold a reference to the extracted class).

## Extract Interface / Extract Superclass

Create an interface or base class from an existing class's public methods.

**Safe because:** Existing code continues to use the concrete class. The new interface/superclass adds a seam for testing without changing callers.

## Introduce Parameter

Replace a hardcoded value inside a method with a parameter, passing the original value at all call sites.

**Safe because:** Every call site passes the same value as before. Behavior is identical.

## Introduce Parameter Object

Group multiple related parameters into a single object parameter. All call sites construct the object with the same values.

**Safe because:** Same values flow through, just packaged differently.

**Watch for:** Ensure all call sites are updated. If a call site is missed, it will fail to compile (which is actually a safety feature).

## Slide Statements

Move adjacent statements closer to where their result is used, without crossing any dependencies.

**Safe because:** Reordering independent statements doesn't change results.

**Watch for:** Side effects. If statement A writes to something statement B reads, they are not independent.

## Split Loop

Take a loop that does two things and split it into two loops over the same collection.

**Safe because:** Same operations happen on the same elements. Order within each concern is preserved.

**Watch for:** Interleaved dependencies — if iteration N of concern A depends on iteration N of concern B happening first, splitting breaks it. Also note this doubles iteration count, which may matter in hot paths.

## Encapsulate Field

Replace direct field access with getter/setter methods.

**Safe because:** The getter returns the field, the setter assigns it. Same reads and writes.

## Replace Magic Number / String with Named Constant

Extract a literal value into a named constant.

**Safe because:** Same value, just named. The compiler substitutes it identically.

## Remove Dead Code

Delete code that is unreachable or never called.

**Safe because:** By definition, unreachable code cannot affect behavior.

**Watch for:** Verify the code is truly unreachable, not just rarely reached. Use grep to confirm no callers exist. Reflection-based invocation (e.g., MEF plugins, dependency injection by name) may not show up in a text search.

---

## When to Use These

These refactorings are **entry points** — use them to create seams, improve testability, and make code understandable enough to write characterization tests. Once you have test coverage, proceed with larger structural refactorings under the full process described in the main skill.
