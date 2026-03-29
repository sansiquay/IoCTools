# Phase 6: FluentValidation Documentation Integration - Research

**Researched:** 2026-03-29
**Domain:** Documentation updates for FluentValidation source generator features
**Confidence:** HIGH

## Summary

Phase 6 is a documentation-only phase closing three gaps identified in the v1.5.0 milestone audit. All code being documented already exists and is tested -- this phase adds documentation entries to match existing functionality.

The three gaps are: MISSING-01 (IOC100-102 absent from docs/diagnostics.md, making HelpLinkUri targets dead links), MISSING-02 (validators and validator-graph CLI commands absent from docs/cli-reference.md), and MISSING-03 (FluentValidation test fixture helpers undocumented in docs/testing.md). Each gap has a clear source of truth in the codebase to document from.

**Primary recommendation:** Execute as a single plan with 3 documentation tasks (one per MISSING-* gap), plus a 4th task to update README.md and CHANGELOG.md with FluentValidation mentions.

## Project Constraints (from CLAUDE.md)

- File-scoped namespaces, using inside namespace, var preferred
- 4 spaces, UTF-8, LF, final newline (per .editorconfig)
- HelpLinkUri pattern: docs/diagnostics.md#iocXXX with anchored entries (Phase 01 decision)
- Manual DiagnosticCatalog approach (Phase 02 decision)
- Category-based navigation in diagnostics.md (Phase 04 decision)
- Severity badges and cross-references in diagnostics (Phase 04 decision)

## Architecture Patterns

### Documentation Structure Already Established

All three target documentation files have well-established patterns from Phase 04. New entries must follow existing conventions exactly.

### Pattern 1: Diagnostic Entry Format (diagnostics.md)

**What:** Each diagnostic gets a consistent anchored section with severity badge, category, cause, fix, example, and related links.
**When to use:** Adding IOC100, IOC101, IOC102 entries.
**Source of truth:** `FluentValidationDiagnosticDescriptors.cs` for severity, title, message format, description, and category.

```markdown
### IOC100

**Severity:** [!Warning](#) | **Category:** IoCTools.FluentValidation

**Cause:** {from descriptor title/description}

**Fix:** {remediation guidance}

**Example:**
\```csharp
// Before (direct instantiation):
{bad pattern}

// After (injected):
{good pattern}
\```

**Related:** [IOC101](#ioc101), [IOC102](#ioc102)

---
```

Key details from descriptors:
- **IOC100** (Warning): "Validator directly instantiates DI-managed child validator" -- fires when `SetValidator(new ChildValidator())` is used instead of injecting via constructor
- **IOC101** (Warning): "Validator composition creates lifetime mismatch" -- captive dependency pattern for validators (Singleton parent with Scoped/Transient child)
- **IOC102** (Error): "Validator class missing partial modifier" -- same pattern as IOC080 but for AbstractValidator subclasses

### Pattern 2: CLI Command Entry Format (cli-reference.md)

**What:** Each CLI command gets a section with bash example, options table, and output description.
**When to use:** Adding `validators` and `validator-graph` command documentation.

From codebase inspection:

**`validators` command:**
- Options: `--project` (required), `--filter` (optional type/model filter), `--json`, `--verbose`
- Output: Lists all FluentValidation validator classes with lifetime, model type, and composition edge count
- JSON mode: Array of `{ validator, modelType, lifetime, hasComposition, compositionEdges[] }`

**`validator-graph` command:**
- Options: `--project` (required), `--why <validator>` (optional trace mode), `--json`, `--verbose`
- Output: Tree visualization of validator composition hierarchy (SetValidator/Include/SetInheritanceValidator chains)
- `--why` mode: Traces why a validator has its lifetime through composition chains
- JSON mode: Nested tree with `{ validator, modelType, lifetime, children[] }`

### Pattern 3: Testing Feature Section (testing.md)

**What:** FluentValidation-aware setup helpers in test fixtures.
**When to use:** Adding section about `SetupValidationSuccess()`/`SetupValidationFailure()` helpers.

From `FluentValidationFixtureHelper.cs`:
- **Gate:** Helpers only generated when FluentValidation is in compilation references
- **Detection:** Name-based `IValidator<T>` detection (no FV package dependency in generator)
- **Generated methods per IValidator<T> parameter:**
  - `Setup{ParamName}ValidationSuccess()` -- sets up both `Validate()` and `ValidateAsync()` to return empty `ValidationResult`
  - `Setup{ParamName}ValidationFailure(params string[] errorMessages)` -- sets up both sync/async to return `ValidationResult` with failures

### Navigation Updates Required

The diagnostics.md category index (lines 6-12) needs a new category entry:
```markdown
- [FluentValidation Diagnostics](#fluentvalidation-diagnostics) - IOC100-IOC102
```

The cli-reference.md needs `validators` and `validator-graph` in the command listing.

The testing.md "Related" section should cross-reference the new FluentValidation CLI commands.

### Anti-Patterns to Avoid

- **Inconsistent formatting:** Do not deviate from the established section format -- copy the exact markdown patterns from existing entries
- **Missing anchors:** Every diagnostic MUST have an `### IOC1XX` anchor that matches the HelpLinkUri pattern `#ioc1xx`
- **Undocumented options:** CLI command docs must list ALL options shown in `CommandLineParser.cs`

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Diagnostic descriptions | Writing from scratch | Copy from `FluentValidationDiagnosticDescriptors.cs` | Descriptors contain exact title, message format, and description text |
| CLI option lists | Guessing options | Read from `CommandLineParser.cs` ParseValidators/ParseValidatorGraph | Source of truth for available flags |
| Fixture helper signatures | Guessing API | Read from `FluentValidationFixtureHelper.cs` | Exact method signatures and behavior documented in code |

## Common Pitfalls

### Pitfall 1: Broken anchor links
**What goes wrong:** HelpLinkUri in FluentValidationDiagnosticDescriptors.cs points to `#ioc100`, `#ioc101`, `#ioc102` -- if anchors use wrong case or format, links stay broken.
**Why it happens:** GitHub auto-generates anchors from headings in lowercase with hyphens.
**How to avoid:** Use `### IOC100` as the heading (GitHub renders as `#ioc100`). Verify by checking existing pattern -- `### IOC090` renders as `#ioc090`.
**Warning signs:** Clicking HelpLinkUri in IDE still leads to 404.

### Pitfall 2: Missing category in diagnostics.md navigation
**What goes wrong:** IOC100-102 entries exist but aren't listed in the category index at the top of diagnostics.md.
**Why it happens:** Forgetting to update the table of contents.
**How to avoid:** Add a "FluentValidation Diagnostics" category to the index list on lines 6-12.

### Pitfall 3: Inconsistent severity badges
**What goes wrong:** Using wrong severity indicator for diagnostics.
**Why it happens:** IOC100 and IOC101 are Warning, IOC102 is Error -- easy to mix up.
**How to avoid:** Cross-reference `FluentValidationDiagnosticDescriptors.cs` for exact `DiagnosticSeverity` values.

### Pitfall 4: Omitting async validation setup
**What goes wrong:** Documenting only `SetupValidationSuccess()` sync behavior but omitting async.
**Why it happens:** The helper sets up BOTH `Validate()` and `ValidateAsync()` -- this is a key feature worth documenting.
**How to avoid:** Show both sync and async setup in the example code.

## Code Examples

### IOC100 diagnostic example (from descriptor)
```csharp
// BAD: Direct instantiation bypasses DI
[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(o => o.Address)
            .SetValidator(new AddressValidator()); // IOC100
    }
}

// GOOD: Injected via constructor
[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    [Inject] private readonly AddressValidator _addressValidator;

    partial void OnConstructed()
    {
        RuleFor(o => o.Address)
            .SetValidator(_addressValidator);
    }
}
```

### IOC101 diagnostic example (from descriptor)
```csharp
// BAD: Singleton captures Scoped child
[Singleton]
public partial class OrderValidator : AbstractValidator<Order>
{
    [Inject] private readonly AddressValidator _addressValidator; // IOC101 if AddressValidator is Scoped
}

// GOOD: Match lifetimes
[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    [Inject] private readonly AddressValidator _addressValidator;
}
```

### IOC102 diagnostic example (from descriptor)
```csharp
// BAD: Missing partial
[Scoped]
public class OrderValidator : AbstractValidator<Order> // IOC102
{
    [Inject] private readonly IOrderRepository _repo;
}

// GOOD: Add partial
[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    [Inject] private readonly IOrderRepository _repo;
}
```

### CLI validators command output
```bash
ioc-tools validators --project MyProject.csproj

# Output:
# Validators: 3
#
#   [Scoped] MyApp.OrderValidator -> Order (2 composition edges)
#   [Scoped] MyApp.AddressValidator -> Address
#   [Transient] MyApp.CustomerValidator -> Customer
```

### CLI validator-graph command output
```bash
ioc-tools validator-graph --project MyProject.csproj

# Output:
# OrderValidator [Scoped] -> Order
# +-- AddressValidator [Scoped] -> Address (via SetValidator (injected))
# +-- CustomerValidator [Transient] -> Customer (via Include (injected))
```

### CLI validator-graph --why output
```bash
ioc-tools validator-graph --project MyProject.csproj --why OrderValidator

# Output:
# MyApp.OrderValidator is Scoped because:
#   - composes MyApp.AddressValidator [Scoped] via SetValidator (matching lifetime)
```

### FluentValidation test fixture helpers
```csharp
// Production code
[Scoped]
[DependsOn<IValidator<Order>, IOrderRepository>]
public partial class OrderHandler
{
    public async Task Handle(Order order)
    {
        var result = await _validator.ValidateAsync(order);
        if (!result.IsValid) throw new ValidationException(result.Errors);
        await _orderRepository.Save(order);
    }
}

// Test code
[Cover<OrderHandler>]
public partial class OrderHandlerTests
{
    [Fact]
    public async Task Handle_ValidOrder_Saves()
    {
        // Generated helper -- sets up both Validate() and ValidateAsync()
        SetupValidatorValidationSuccess();

        var sut = CreateSut();
        await sut.Handle(new Order());

        // Verify save was called
    }

    [Fact]
    public async Task Handle_InvalidOrder_Throws()
    {
        // Generated helper with custom error messages
        SetupValidatorValidationFailure("Name is required", "Amount must be positive");

        var sut = CreateSut();
        await Assert.ThrowsAsync<ValidationException>(() => sut.Handle(new Order()));
    }
}
```

## Files to Modify

| File | Change | Source of Truth |
|------|--------|-----------------|
| `docs/diagnostics.md` | Add FluentValidation Diagnostics category + IOC100, IOC101, IOC102 entries | `FluentValidationDiagnosticDescriptors.cs` |
| `docs/cli-reference.md` | Add `validators` and `validator-graph` command sections | `Program.cs`, `CommandLineParser.cs`, `ValidatorInspector.cs`, `ValidatorPrinter.cs` |
| `docs/testing.md` | Add FluentValidation section with SetupValidationSuccess/Failure helpers | `FluentValidationFixtureHelper.cs`, `FixtureEmitter.cs` |
| `README.md` | Add FluentValidation mention to feature list | N/A |
| `CHANGELOG.md` | Add FluentValidation entries to v1.5.0 section | N/A |

## Open Questions

1. **Should docs/diagnostics.md category index use "FluentValidation Diagnostics" or "Validation Diagnostics"?**
   - What we know: The descriptor category is `IoCTools.FluentValidation`. Existing categories are named after their subcategory (e.g., "Dependency Diagnostics", "Registration Diagnostics").
   - Recommendation: Use "FluentValidation Diagnostics" to match the `IoCTools.FluentValidation` category name pattern.

2. **Should a new docs/fluentvalidation.md page be created or should FV content go in existing files?**
   - What we know: The audit specifically says to update existing files (diagnostics.md, cli-reference.md, testing.md). The MISSING items are about gaps in existing pages.
   - Recommendation: Add to existing files per audit specification. A dedicated FV page can be a future enhancement if content grows.

## Sources

### Primary (HIGH confidence)
- `FluentValidationDiagnosticDescriptors.cs` -- exact diagnostic IDs, severities, titles, messages, descriptions
- `ValidatorInspector.cs` -- CLI validators discovery logic, composition edge detection
- `ValidatorPrinter.cs` -- CLI output format for validators list, graph, and why modes
- `CommandLineParser.cs` -- CLI option definitions for ParseValidators and ParseValidatorGraph
- `FluentValidationFixtureHelper.cs` -- test fixture FV helper generation logic
- `FixtureEmitter.cs` -- FV-aware fixture emission integration
- `docs/diagnostics.md` -- existing diagnostic entry format pattern (1267 lines)
- `docs/cli-reference.md` -- existing CLI command documentation format (273 lines)
- `docs/testing.md` -- existing testing documentation format (345 lines)
- `.planning/v1.5.0-MILESTONE-AUDIT.md` -- MISSING-01, MISSING-02, MISSING-03 definitions

## Metadata

**Confidence breakdown:**
- Diagnostic entries (MISSING-01): HIGH -- descriptor source code contains exact text, existing format well-established
- CLI command docs (MISSING-02): HIGH -- command implementation fully inspected, option parsing verified
- Testing helpers (MISSING-03): HIGH -- helper code directly read, generation logic understood
- README/CHANGELOG updates: HIGH -- mechanical additions following existing patterns

**Research date:** 2026-03-29
**Valid until:** Indefinite (documentation of existing stable features)
