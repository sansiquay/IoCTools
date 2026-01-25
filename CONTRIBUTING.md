# Contributing to IoCTools

Thank you for your interest in contributing to IoCTools! This document explains the multi-repo workflow and where different types of contributions should go.

## Multi-Repository Structure

IoCTools development spans two repositories:

1. **IoCTools** (this repo) - Project code, source generator, abstractions, CLI, tests
2. **claude-config** (`~/.claude` git repo) - Global workflow configuration, beads documentation, skills

### What Goes Where

| Contribution Type | Repository | Location |
|-------------------|------------|----------|
| Source generator code | IoCTools | `IoCTools.Generator/` |
| Diagnostic descriptors | IoCTools | `IoCTools.Generator/Generator/Diagnostics/` |
| Attribute definitions | IoCTools | `IoCTools.Abstractions/` |
| CLI tooling | IoCTools | `IoCTools.Tools.Cli/` |
| Unit/integration tests | IoCTools | `IoCTools.Generator.Tests/` |
| Sample code | IoCTools | `IoCTools.Sample/` |
| Beads workflow docs | claude-config | `~/.claude/skills/gemini/references/beads-context.md` |
| Issue tracking templates | claude-config | `~/.claude/skills/beads-*` |
| Claude Code skills | claude-config | `~/.claude/skills/` |

## Git Workflow

### Project Code (IoCTools repo)

```bash
cd ~/Documents/projects/IoCTools
git checkout -b feature/your-feature-name
# Make changes
git add .
git commit -m "feat: description of changes"
git push origin feature/your-feature-name
# Create PR on GitHub
```

### Workflow Documentation (claude-config repo)

```bash
cd ~/.claude
git checkout -b docs/update-workflow
# Make changes to beads-context.md or skills/
git add .
git commit -m "docs: update workflow documentation"
git push origin docs/update-workflow
# Create PR on GitHub
```

## Development Workflow

1. **Find work**: Use `bd ready` to see available tasks (beads workflow)
2. **Claim task**: Use `bd update <id> --status=in_progress`
3. **Make changes**: Edit files in appropriate repository
4. **Run tests**: `dotnet test` in IoCTools root
5. **Close task**: `bd close <id> --reason="completed..."`
6. **Sync and push**: `bd sync && git push`

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
- For workflow/beads questions: Update or reference beads-context.md in claude-config repo
- For Claude Code skills: See `~/.claude/skills/` directory
