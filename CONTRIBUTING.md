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

## Diagnostic Guidelines

When adding new diagnostics:

1. Add descriptor to `DiagnosticDescriptors.cs`
2. Implement validator in `IoCTools.Generator/Generator/Diagnostics/Validators/`
3. Add tests in `IoCTools.Generator.Tests/`
4. Update `README.md` diagnostic reference table
5. Document severity configurability if applicable

## Questions?

- For project-specific questions: Create an issue in IoCTools repo
