# Contributing to IoCTools

Thank you for your interest in contributing to IoCTools!

## Getting Started

```bash
cd ~/Documents/projects/IoCTools
git checkout -b feature/your-feature-name
# Make changes
git add .
git commit -m "feat: description of changes"
git push origin feature/your-feature-name
# Create PR on GitHub
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test IoCTools.Generator.Tests

# Run with detailed output
dotnet test --verbosity normal
```

## Code Style

- Follow C# naming conventions (PascalCase for public members, _camelCase for private fields)
- Add XML documentation comments for public APIs
- Keep methods focused and small
- Use `[Scoped]`, `[Singleton]`, `[Transient]` attributes for service lifetime
- Never use reflection for dependency injection - use the source generator

## Platform Constraints

IoCTools targets `netstandard2.0` for the generator internally, but your service code can use any .NET version and C# features. This distinction is important for contributors:

- **Generator code** (`IoCTools.Generator`, `IoCTools.Abstractions`) is limited to netstandard2.0 APIs
- **User service code** has no constraints from IoCTools

When contributing to the generator, avoid:
- Record types (`record struct`) - use classes/structs with manual equality
- `HashCode.Combine()` - use manual hash code implementation
- `Span<T>`/`Memory<T>` - limited availability in netstandard2.0

See [Platform Constraints Documentation](docs/platform-constraints.md) for full details on limitations and workarounds.

## Diagnostic Guidelines

When adding new diagnostics:

1. Add descriptor to `DiagnosticDescriptors.cs` with HelpLinkUri pointing to `docs/diagnostics.md#iocXXX`
2. Implement validator in `IoCTools.Generator/Generator/Diagnostics/Validators/`
3. Add tests in `IoCTools.Generator.Tests/`
4. Update `docs/diagnostics.md` with the new diagnostic entry (including category, severity, cause, fix, examples)
5. Update README.md Error-only diagnostic table if severity is Error
6. Document severity configurability via MSBuild properties if applicable

**HelpLinkUri format:** `https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#iocXXX`

Ensure the anchor `#iocXXX` exists in docs/diagnostics.md before committing.

## Questions?

- For project-specific questions: Create an issue in IoCTools repo
