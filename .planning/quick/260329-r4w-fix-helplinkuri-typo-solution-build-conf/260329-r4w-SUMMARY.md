---
quick_id: 260329-r4w
description: Fix HelpLinkUri typo, solution build configs, duplicate diagnostic headings, add TDIAG to catalog
date: 2026-03-29
one_liner: Fixed 4 post-milestone integration issues — HelpLinkUri URLs, solution build configs, duplicate doc headings, and TDIAG catalog entries
---

# Quick Task Summary

## Completed

1. **HelpLinkUri typo fixed** — Replaced `nathan/p-lane` with `nathan-p-lane` in 5 descriptors (IOC086, IOC090-092, IOC094) in RegistrationDiagnostics.cs. IDE help links now resolve correctly.

2. **Solution build configs added** — Added ProjectConfigurationPlatforms entries for IoCTools.Testing.Abstractions, IoCTools.Testing, and IoCTools.Testing.Tests in IoCTools.sln. These 3 projects now build with `dotnet build IoCTools.sln`.

3. **Duplicate diagnostic headings removed** — Removed 5 duplicate `### IOC*` headings from docs/diagnostics.md (IOC041/042 from Structural section, IOC043/044 from Configuration section, IOC079 from Dependency section). Each diagnostic now has exactly one heading, fixing HelpLinkUri anchor resolution.

4. **TDIAG entries added to CLI catalog** — Added TDIAG-01 through TDIAG-05 to DiagnosticCatalog.cs with IoCTools.Testing category. `ioc-tools suppress` can now target test fixture diagnostics.

## Files Changed

- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs` — URL fix
- `IoCTools.sln` — Build config entries for 3 projects
- `docs/diagnostics.md` — Removed 5 duplicate headings
- `IoCTools.Tools.Cli/Utilities/DiagnosticCatalog.cs` — Added 5 TDIAG entries
