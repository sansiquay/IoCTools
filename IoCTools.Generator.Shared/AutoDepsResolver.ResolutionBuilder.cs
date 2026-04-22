namespace IoCTools.Generator.Shared;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

public static partial class AutoDepsResolver
{
    private sealed class ResolutionBuilder
    {
        private readonly Compilation _compilation;
        private readonly INamedTypeSymbol _service;

        // Keyed by SymbolIdentity of dep type.
        private readonly Dictionary<SymbolIdentity, List<AutoDepAttribution>> _entries =
            new Dictionary<SymbolIdentity, List<AutoDepAttribution>>();

        private readonly HashSet<SymbolIdentity> _optOutClosedTypes = new HashSet<SymbolIdentity>();
        private readonly HashSet<SymbolIdentity> _optOutOpenShapes = new HashSet<SymbolIdentity>();
        private bool _blanketOptOut;

        public ResolutionBuilder(Compilation compilation, INamedTypeSymbol service)
        {
            _compilation = compilation;
            _service = service;
            CollectServiceOptOuts();
        }

        private void CollectServiceOptOuts()
        {
            // [NoAutoDeps] on any partial -> blanket
            // [NoAutoDep<T>] on any partial -> closed type
            // [NoAutoDepOpen(typeof(T<>))] on any partial -> open shape
            //
            // Partials share ISymbol -- _service.GetAttributes() returns the union of attributes
            // across every partial class file. No explicit per-file iteration required.
            foreach (var a in _service.GetAttributes())
            {
                var cls = a.AttributeClass;
                if (cls is null) continue;
                var ns = cls.ContainingNamespace?.ToDisplayString();
                if (ns != "IoCTools.Abstractions.Annotations") continue;

                var attrName = cls.Name;
                if (attrName == "NoAutoDepsAttribute")
                {
                    _blanketOptOut = true;
                }
                else if (attrName == "NoAutoDepAttribute" && cls.TypeArguments.Length == 1)
                {
                    if (cls.TypeArguments[0] is ITypeSymbol t)
                        _optOutClosedTypes.Add(SymbolIdentity.From(t));
                }
                else if (attrName == "NoAutoDepOpenAttribute" &&
                         a.ConstructorArguments.Length == 1 &&
                         a.ConstructorArguments[0].Value is ITypeSymbol openShape)
                {
                    _optOutOpenShapes.Add(SymbolIdentity.From(openShape));
                }
            }
        }

        public void AddBuiltinILoggerIfAvailable()
        {
            if (_blanketOptOut) return;
            var ilogger = GetBuiltinILoggerSymbol(_compilation);
            if (ilogger is null) return;
            if (_optOutOpenShapes.Contains(SymbolIdentity.From(ilogger))) return;
            var closed = ilogger.Construct(_service);
            AddEntry(
                SymbolIdentity.From(closed),
                new AutoDepAttribution(AutoDepSourceKind.AutoBuiltinILogger, sourceName: null, assemblyName: null));
        }

        public void AddUniversalFromAttributes()
        {
            if (_blanketOptOut) return;
            foreach (var entry in EnumerateAutoDepAttributes(_compilation, includeTransitive: true))
            {
                var a = entry.Attribute;
                var cls = a.AttributeClass;
                if (cls is null) continue;
                var attrName = cls.Name;

                if (attrName == "AutoDepAttribute" && cls.TypeArguments.Length == 1)
                {
                    if (cls.TypeArguments[0] is ITypeSymbol depType)
                    {
                        AddDepWithAttribution(
                            depType,
                            entry.IsTransitive,
                            AutoDepSourceKind.AutoUniversal,
                            entry.DeclaringAssembly.Identity.Name);
                    }
                }
                else if (attrName == "AutoDepOpenAttribute" &&
                         a.ConstructorArguments.Length >= 1 &&
                         a.ConstructorArguments[0].Value is INamedTypeSymbol unbound &&
                         unbound.IsUnboundGenericType)
                {
                    if (unbound.TypeParameters.Length != 1) continue; // IOC100 handled in diagnostics
                    var closed = unbound.OriginalDefinition.Construct(_service);
                    AddDepWithAttribution(
                        closed,
                        entry.IsTransitive,
                        AutoDepSourceKind.AutoOpenUniversal,
                        entry.DeclaringAssembly.Identity.Name);
                }
            }
        }

        public void ApplyProfiles()
        {
            if (_blanketOptOut) return;

            // `attachedProfiles`: profile identity -> `isTransitiveAttachment` (true if the
            // attachment rule came from a referenced assembly). Service-level [AutoDeps<T>] is
            // always local. For AutoDepsApply / AutoDepsApplyGlob, the attachment rule may come
            // from a transitive assembly; however the per-dep transitivity used for attribution
            // is determined by each AutoDepIn's own IsTransitive flag, not by the attachment's
            // origin (a local attachment still sees transitive AutoDepIn contributions, and
            // vice versa).
            var attachedProfiles = new HashSet<SymbolIdentity>();

            // 1. [AutoDeps<TProfile>] on the service (always local attachment)
            foreach (var a in _service.GetAttributes())
            {
                var cls = a.AttributeClass;
                if (cls is null) continue;
                var ns = cls.ContainingNamespace?.ToDisplayString();
                if (ns != "IoCTools.Abstractions.Annotations") continue;
                if (cls.Name == "AutoDepsAttribute" && cls.TypeArguments.Length == 1 &&
                    cls.TypeArguments[0] is ITypeSymbol profile)
                {
                    attachedProfiles.Add(SymbolIdentity.From(profile));
                }
            }

            // 2. [assembly: AutoDepsApply<TProfile, TBase>] matching service's base or implemented interface
            // 3. [assembly: AutoDepsApplyGlob<TProfile>("pattern")] matching service's namespace
            foreach (var entry in EnumerateAutoDepAttributes(_compilation, includeTransitive: true))
            {
                var a = entry.Attribute;
                var cls = a.AttributeClass;
                if (cls is null) continue;
                var attrName = cls.Name;

                if (attrName == "AutoDepsApplyAttribute" && cls.TypeArguments.Length == 2)
                {
                    if (cls.TypeArguments[0] is ITypeSymbol prof &&
                        cls.TypeArguments[1] is ITypeSymbol tbase &&
                        ServiceMatchesBase(tbase))
                    {
                        attachedProfiles.Add(SymbolIdentity.From(prof));
                    }
                }
                else if (attrName == "AutoDepsApplyGlobAttribute" &&
                         cls.TypeArguments.Length == 1 &&
                         a.ConstructorArguments.Length >= 1 &&
                         a.ConstructorArguments[0].Value is string pattern)
                {
                    if (cls.TypeArguments[0] is ITypeSymbol prof)
                    {
                        var ns = _service.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                        if (GlobMatch(ns, pattern, out _))
                        {
                            attachedProfiles.Add(SymbolIdentity.From(prof));
                        }
                    }
                }
            }

            if (attachedProfiles.Count == 0) return;

            // For each attached profile, find [assembly: AutoDepIn<TProfile, T>] contributions
            // (local + transitive) across all assemblies.
            foreach (var profileId in attachedProfiles)
            {
                foreach (var entry in EnumerateAutoDepAttributes(_compilation, includeTransitive: true))
                {
                    var a = entry.Attribute;
                    var cls = a.AttributeClass;
                    if (cls is null) continue;
                    if (cls.Name != "AutoDepInAttribute") continue;
                    if (cls.TypeArguments.Length != 2) continue;
                    if (cls.TypeArguments[0] is not ITypeSymbol profArgSym) continue;
                    var profArg = SymbolIdentity.From(profArgSym);
                    if (!profArg.Equals(profileId)) continue;
                    if (cls.TypeArguments[1] is not ITypeSymbol depType) continue;

                    AddDepWithAttribution(
                        depType,
                        entry.IsTransitive,
                        AutoDepSourceKind.AutoProfile,
                        sourceName: profileId.MetadataName);
                }
            }
        }

        public void ApplyOptOuts()
        {
            // Step 4 first: [NoAutoDeps] wipes everything.
            if (_blanketOptOut)
            {
                _entries.Clear();
                return;
            }

            // Step 3a: [NoAutoDep<T>] removes matching closed-type entries.
            foreach (var id in _optOutClosedTypes.ToList())
            {
                _entries.Remove(id);
            }

            // Step 3b: [NoAutoDepOpen(typeof(T<>))] removes entries whose open-generic shape
            // matches, regardless of the closure.
            if (_optOutOpenShapes.Count > 0)
            {
                var toRemove = new List<SymbolIdentity>();
                foreach (var kv in _entries)
                {
                    if (EntryMatchesOpenShape(kv.Key, _optOutOpenShapes))
                        toRemove.Add(kv.Key);
                }
                foreach (var r in toRemove) _entries.Remove(r);
            }
        }

        public void ReconcileAgainstDependsOn()
        {
            // Stub: implemented in Increment D (step 5 of the resolution order).
        }

        public AutoDepsResolverOutput Build()
        {
            var entries = _entries
                .Select(kv => new AutoDepResolvedEntry(kv.Key, kv.Value.ToImmutableArray()))
                .ToImmutableArray();
            return new AutoDepsResolverOutput(entries);
        }

        private void AddDepWithAttribution(
            ITypeSymbol depType,
            bool isTransitive,
            AutoDepSourceKind kind,
            string? sourceName)
        {
            var id = SymbolIdentity.From(depType);
            if (_optOutClosedTypes.Contains(id)) return;

            // When the contribution came from a referenced assembly, the attribution collapses
            // to AutoTransitive regardless of which universal/profile kind produced it. The
            // `sourceName` parameter is a pun at that point:
            //   - Universal (AutoUniversal/AutoOpenUniversal): sourceName is the declaring
            //     assembly name (used as AssemblyName when transitive).
            //   - Profile (AutoProfile): sourceName is the profile's metadata name. For a
            //     transitive profile contribution, we still want AutoTransitive + AssemblyName;
            //     the profile name is informational and kept in SourceName.
            if (isTransitive)
            {
                // For universal transitive, the sourceName IS the assembly name.
                // For profile transitive, sourceName is the profile name; we don't track the
                // declaring assembly separately here -- callers pass assembly via sourceName
                // for universal, and profile name for profile. Use sourceName for both slots
                // when universal; keep profile name in SourceName for profile-transitive.
                if (kind == AutoDepSourceKind.AutoProfile)
                {
                    AddEntry(id, new AutoDepAttribution(
                        AutoDepSourceKind.AutoTransitive,
                        sourceName,
                        id.ContainingAssemblyName));
                }
                else
                {
                    AddEntry(id, new AutoDepAttribution(
                        AutoDepSourceKind.AutoTransitive,
                        sourceName,
                        sourceName));
                }
            }
            else
            {
                AddEntry(id, new AutoDepAttribution(kind, sourceName, null));
            }
        }

        private void AddEntry(SymbolIdentity id, AutoDepAttribution attribution)
        {
            if (!_entries.TryGetValue(id, out var list))
            {
                list = new List<AutoDepAttribution>();
                _entries[id] = list;
            }
            // Dedup by attribution equality
            if (!list.Contains(attribution)) list.Add(attribution);
        }

        private bool ServiceMatchesBase(ITypeSymbol tbase)
        {
            // Base class chain
            INamedTypeSymbol? current = _service.BaseType;
            while (current is not null)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, tbase.OriginalDefinition))
                    return true;
                current = current.BaseType;
            }
            // Interface implementation
            foreach (var iface in _service.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, tbase.OriginalDefinition))
                    return true;
            }
            return false;
        }

        private static bool EntryMatchesOpenShape(SymbolIdentity entryId, HashSet<SymbolIdentity> openShapes)
        {
            // A closed generic type's fully-qualified display name ends with `<...>`.
            // The corresponding open shape has the same head (up through the type name) and
            // containing assembly. Strategy: compare heads (everything before '<').
            var entryHead = StripArguments(entryId.MetadataName);
            foreach (var open in openShapes)
            {
                var openHead = StripArguments(open.MetadataName);
                if (string.Equals(entryHead, openHead, StringComparison.Ordinal) &&
                    string.Equals(entryId.ContainingAssemblyName, open.ContainingAssemblyName, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string StripArguments(string displayName)
        {
            int lt = displayName.IndexOf('<');
            return lt < 0 ? displayName : displayName.Substring(0, lt);
        }

        public static bool HasManualConstructor(INamedTypeSymbol service)
        {
            // A user-authored constructor appears as a non-implicit IMethodSymbol on the class.
            // Mirrors what IoCTools today treats as "user-authored constructor."
            foreach (var ctor in service.InstanceConstructors)
            {
                if (!ctor.IsImplicitlyDeclared) return true;
            }
            return false;
        }
    }
}
