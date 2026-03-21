# IoCTools Diagnostics Reference

Quick reference for all IoCTools diagnostic messages. For detailed examples and guidance, see the full documentation.

## IOC001

**Severity:** Error
**Category:** IoCTools.Dependency

**Cause:** No implementation of the depended-upon interface exists in the project.

**Fix:** Create a class implementing the interface with a lifetime attribute, add `[ExternalService]`, or register manually.

---

## IOC002

**Severity:** Error
**Category:** IoCTools.Dependency

**Cause:** An implementation of the interface exists but lacks a lifetime attribute and is not registered.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` to the implementation, add `[ExternalService]`, or register manually.

---

## IOC003

**Severity:** Error
**Category:** IoCTools.Dependency

**Cause:** A circular dependency chain was detected between services.

**Fix:** Break the cycle by using interfaces, introducing a mediator pattern, or refactoring to eliminate the circular reference.

---

## IOC004

**Severity:** Error
**Category:** IoCTools.Registration

**Cause:** `[RegisterAsAll]` is used on a class without a lifetime attribute.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` to the class.

---

## IOC005

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** `[SkipRegistration]` is used without `[RegisterAsAll]`, making it ineffective.

**Fix:** Add `[RegisterAsAll]` to make `[SkipRegistration]` meaningful, or remove the unnecessary `[SkipRegistration]`.

---

## IOC006

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** The same type appears in multiple `[DependsOn]` attributes on the same class.

**Fix:** Remove duplicate dependency declarations.

---

## IOC007

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** A type is declared in both `[DependsOn]` and as an `[Inject]` field on the same class.

**Fix:** Remove either the `[DependsOn]` declaration or the `[Inject]` field.

---

## IOC008

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** The same type appears multiple times in a single `[DependsOn]` attribute.

**Fix:** Remove duplicate type declarations from the `[DependsOn]` attribute.

---

## IOC009

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** `[SkipRegistration]` specifies an interface not registered by `[RegisterAsAll]`.

**Fix:** Remove the unnecessary `[SkipRegistration]` declaration.

---

## IOC010

**Severity:** Warning
**Category:** IoCTools.Structural

**Cause:** Background service has a non-Singleton lifetime (deprecated diagnostic).

**Fix:** Use IOC014 instead. This diagnostic has been consolidated into IOC014.

---

## IOC011

**Severity:** Error
**Category:** IoCTools.Structural

**Cause:** A background service class inherits from `BackgroundService` but is not marked as `partial`.

**Fix:** Add `partial` modifier to the class declaration.

---

## IOC012

**Severity:** Error
**Category:** IoCTools.Lifetime

**Cause:** A Singleton service depends on a Scoped service, which would cause the scoped dependency to be captured for the lifetime of the singleton.

**Fix:** Change the dependency to `[Singleton]`, change this service to `[Scoped]` or `[Transient]`, inject `IServiceProvider` and call `CreateScope()` to resolve on demand, or use a factory delegate.

---

## IOC013

**Severity:** Warning
**Category:** IoCTools.Lifetime

**Cause:** A Singleton service depends on a Transient service, capturing only one instance for the singleton's lifetime.

**Fix:** Change the dependency to `[Singleton]` if it should be shared, inject `IServiceProvider` and call `CreateScope()` to resolve on demand, or use a factory delegate `Func<T>`.

---

## IOC014

**Severity:** Error
**Category:** IoCTools.Lifetime

**Cause:** A background service has a non-Singleton lifetime.

**Fix:** Change to `[Singleton]` for optimal background service lifetime, or suppress the warning if the current lifetime is intentional.

---

## IOC015

**Severity:** Error
**Category:** IoCTools.Lifetime

**Cause:** A service lifetime mismatch exists in the inheritance chain, with the full inheritance path shown in the diagnostic.

**Fix:** Make all services in the chain use the same lifetime, change the consuming service lifetime, or break the inheritance chain.

---

## IOC016

**Severity:** Error
**Category:** IoCTools.Configuration

**Cause:** A configuration key is invalid (empty, whitespace-only, or contains invalid characters like double colons).

**Fix:** Provide a valid configuration key. Example valid keys: `ConnectionStrings:Default`, `App:Settings:Feature`.

---

## IOC017

**Severity:** Warning
**Category:** IoCTools.Configuration

**Cause:** The configuration type cannot be bound from configuration.

**Fix:** Use a supported type: primitives (`string`, `int`, `bool`, `double`), POCOs with parameterless constructors, or collections (`List<T>`, `Dictionary<string, T>`).

---

## IOC018

**Severity:** Error
**Category:** IoCTools.Configuration

**Cause:** `[InjectConfiguration]` is used on a class that is not marked as `partial`.

**Fix:** Add `partial` modifier to the class declaration to enable configuration injection constructor generation.

---

## IOC019

**Severity:** Warning
**Category:** IoCTools.Configuration

**Cause:** `[InjectConfiguration]` is applied to a static field, which is not supported.

**Fix:** Remove `[InjectConfiguration]` from the static field. Change to an instance field instead.

---

## IOC020

**Severity:** Warning
**Category:** IoCTools.Structural

**Cause:** A conditional service has conflicting conditions (overlapping Environment/NotEnvironment or contradictory Equals/NotEquals).

**Fix:** Ensure conditions do not overlap or contradict each other.

---

## IOC021

**Severity:** Error
**Category:** IoCTools.Structural

**Cause:** `[ConditionalService]` is used without a lifetime attribute.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` to the class.

---

## IOC022

**Severity:** Warning
**Category:** IoCTools.Structural

**Cause:** `[ConditionalService]` is used without specifying any conditions.

**Fix:** Specify at least one Environment, NotEnvironment, or ConfigValue condition.

---

## IOC023

**Severity:** Warning
**Category:** IoCTools.Structural

**Cause:** ConfigValue is specified in `[ConditionalService]` but no Equals or NotEquals comparison is provided.

**Fix:** Add an Equals or NotEquals condition when using ConfigValue.

---

## IOC024

**Severity:** Warning
**Category:** IoCTools.Structural

**Cause:** Equals or NotEquals is specified in `[ConditionalService]` but ConfigValue is missing.

**Fix:** Specify the ConfigValue property to define which configuration key to check.

---

## IOC025

**Severity:** Warning
**Category:** IoCTools.Structural

**Cause:** ConfigValue in `[ConditionalService]` is empty or whitespace-only.

**Fix:** Provide a valid configuration key path for ConfigValue.

---

## IOC026

**Severity:** Warning
**Category:** IoCTools.Structural

**Cause:** Multiple `[ConditionalService]` attributes on the same class may lead to unexpected behavior.

**Fix:** Combine conditions into a single attribute or use separate classes for different conditions.

---

## IOC027

**Severity:** Info
**Category:** IoCTools.Registration

**Cause:** A service may be registered multiple times due to inheritance or attribute combinations.

**Fix:** Review service registration patterns to ensure no unintended duplicates.

---

## IOC028

**Severity:** Error
**Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` is used without service indicators like a lifetime attribute, `[Inject]` fields, or other registration attributes.

**Fix:** Add a lifetime attribute or other service indicators to enable selective interface registration.

---

## IOC029

**Severity:** Error
**Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` specifies an interface that the class does not implement.

**Fix:** Ensure all interfaces specified in `[RegisterAs]` are actually implemented by the class.

---

## IOC030

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` contains a duplicate interface specification.

**Fix:** Remove duplicate interface specifications from the `[RegisterAs]` attribute.

---

## IOC031

**Severity:** Error
**Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` specifies a non-interface type.

**Fix:** `[RegisterAs]` can only specify interface types. Use concrete class types for direct registration.

---

## IOC032

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` is redundant because the class already registers the specified interfaces by default.

**Fix:** Remove the redundant `[RegisterAs]` attribute or reduce the interface list.

---

## IOC033

**Severity:** Warning
**Category:** IoCTools.Lifetime

**Cause:** A class is already implicitly registered as Scoped, making the explicit `[Scoped]` attribute redundant.

**Fix:** Remove the redundant `[Scoped]` attribute or change to a non-default lifetime.

---

## IOC034

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** Both `[RegisterAsAll]` and `[RegisterAs]` are used; `[RegisterAs]` has no effect when `[RegisterAsAll]` is present.

**Fix:** Remove `[RegisterAs]` attributes or drop `[RegisterAsAll]` if selective registration is needed.

---

## IOC035

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** An `[Inject]` field matches the default naming for `[DependsOn]`, making it unnecessarily verbose.

**Fix:** Replace the `[Inject]` field with `[DependsOn<T>]` unless a custom field name or mutability is required.

---

## IOC036

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** A class has multiple lifetime attributes, creating conflicting registrations.

**Fix:** Remove redundant lifetime attributes so only one of `[Scoped]`, `[Singleton]`, or `[Transient]` remains.

---

## IOC037

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** `[SkipRegistration]` is used alongside other registration attributes, preventing them from taking effect.

**Fix:** Remove redundant registration attributes or drop `[SkipRegistration]`.

---

## IOC038

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** `[SkipRegistration]` for interfaces has no effect when `[RegisterAsAll]` is set to DirectOnly mode.

**Fix:** Change `[RegisterAsAll]` to `RegistrationMode.All`/`Exclusionary` or remove the ineffective `[SkipRegistration]`.

---

## IOC039

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** A dependency field is declared via `[Inject]` or `[DependsOn]` but is never referenced in the class.

**Fix:** Remove the unused declaration or reference the generated field in your implementation.

---

## IOC040

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** The same dependency type is declared multiple times via different attributes on the same class.

**Fix:** Declare each dependency once. Prefer `[DependsOn]` and remove duplicate declarations.

---

## IOC041

**Severity:** Error
**Category:** IoCTools.Dependency

**Cause:** A class has both IoCTools dependency declarations and a manual constructor, which conflict.

**Fix:** Let IoCTools generate the constructor, or remove the IoCTools dependency declarations to use manual constructors.

---

## IOC042

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** A dependency is marked `External` but an implementation is already available in the solution.

**Fix:** Remove the `External` flag to let IoCTools manage the dependency normally.

---

## IOC043

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** A dependency uses `IOptions<T>` types directly instead of `[DependsOnConfiguration]`.

**Fix:** Use `[DependsOnConfiguration<T>]` instead of depending on `IOptions`/`IOptionsSnapshot`/`IOptionsMonitor` directly.

---

## IOC044

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** A dependency type is a primitive, value type, or string, which should use configuration injection instead.

**Fix:** Use `[DependsOnConfiguration<T>]` or `[InjectConfiguration]` for configuration values.

---

## IOC045

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** A dependency uses an unsupported collection type.

**Fix:** Use `IReadOnlyCollection<T>` for sets of resolved services.

---

## IOC046

**Severity:** Warning
**Category:** IoCTools.Configuration

**Cause:** The same configuration section is bound in multiple ways (e.g., as options and per-field), creating duplicate sources.

**Fix:** Bind each configuration section exactly once. Avoid mixing options bindings with per-field configuration.

---

## IOC047

**Severity:** Info
**Category:** IoCTools.Registration

**Cause:** An attribute uses a named argument where a params-style constructor argument would be cleaner.

**Fix:** Use params-style constructor arguments for `[DependsOn]` member names and `[DependsOnConfiguration]` keys.

---

## IOC048

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** A dependency is declared as nullable, but dependencies are expected to be required.

**Fix:** Use non-nullable types. Register a no-op implementation if the dependency is optional.

---

## IOC049

**Severity:** Error
**Category:** IoCTools.Dependency

**Cause:** A type implementing `IDependencySet` declares members (methods, properties, fields, events, or nested types).

**Fix:** Keep `IDependencySet` types metadata-only. Move members elsewhere.

---

## IOC050

**Severity:** Error
**Category:** IoCTools.Dependency

**Cause:** Dependency sets form a cycle (e.g., SetA references SetB which references SetA).

**Fix:** Remove one of the set references to break the cycle.

---

## IOC051

**Severity:** Error
**Category:** IoCTools.Dependency

**Cause:** Dependency set expansion produces conflicting member names for the same dependency type.

**Fix:** Align the member names across sets or remove the duplicate dependency.

---

## IOC052

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** A type implementing `IDependencySet` is marked for registration via lifetime or registration attributes.

**Fix:** Remove lifetime/registration attributes. Dependency sets are metadata-only and should not be registered.

---

## IOC053

**Severity:** Info
**Category:** IoCTools.Dependency

**Cause:** The same set of dependencies repeats across multiple services.

**Fix:** Extract the repeated dependencies into an `IDependencySet` and reference it with `[DependsOn<T>]`.

---

## IOC054

**Severity:** Info
**Category:** IoCTools.Dependency

**Cause:** A service already has most members of an existing dependency set.

**Fix:** Adopt the existing dependency set and add the few additional dependencies separately.

---

## IOC055

**Severity:** Info
**Category:** IoCTools.Dependency

**Cause:** Services derived from the same base share common dependencies that could be centralized.

**Fix:** Move shared dependencies into a base-oriented `IDependencySet` or the base class.

---

## IOC056

**Severity:** Info
**Category:** IoCTools.Configuration

**Cause:** A configuration section is bound to both an options type and primitive values.

**Fix:** Use a single binding style per section: either the options object or direct primitives, not both.

---

## IOC057

**Severity:** Warning
**Category:** IoCTools.Configuration

**Cause:** A configuration section referenced by an options type is not bound in the project.

**Fix:** Add `Configure<T>()`, `AddOptions<T>().Bind...`, or implement `IConfigureOptions<T>`.

---

## IOC058

**Severity:** Info
**Category:** IoCTools.Structural

**Cause:** Multiple services deriving from the same base class lack lifetime attributes.

**Fix:** Add a single lifetime attribute to the shared base class to register all derived services in one place.

---

## IOC059

**Severity:** Warning
**Category:** IoCTools.Lifetime

**Cause:** A derived class repeats `[Singleton]` already inherited from a base class.

**Fix:** Remove the redundant `[Singleton]` on the derived class.

---

## IOC060

**Severity:** Warning
**Category:** IoCTools.Lifetime

**Cause:** A derived class repeats `[Transient]` already inherited from a base class.

**Fix:** Remove the redundant `[Transient]` on the derived class.

---

## IOC061

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** A derived class repeats a dependency set already applied in a base class.

**Fix:** Remove the redundant `[DependsOn<Set>]` on the derived class.

---

## IOC062

**Severity:** Info
**Category:** IoCTools.Dependency

**Cause:** Multiple derived services all reference the same dependency set.

**Fix:** Move `[DependsOn<Set>]` to the shared base class to reduce duplication.

---

## IOC063

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** A derived class repeats `[RegisterAs]` interfaces already inherited from a base class.

**Fix:** Remove the redundant `[RegisterAs]` on the derived class.

---

## IOC064

**Severity:** Info
**Category:** IoCTools.Registration

**Cause:** Multiple derived classes repeat the same `[RegisterAs]` interfaces.

**Fix:** Move `[RegisterAs]` to the shared base class to reduce duplication.

---

## IOC065

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** A derived class repeats `[RegisterAsAll]` already inherited from a base class.

**Fix:** Remove the redundant `[RegisterAsAll]` on the derived class.

---

## IOC067

**Severity:** Warning
**Category:** IoCTools.Structural

**Cause:** A derived class repeats `[ConditionalService]` with the same condition as the base class.

**Fix:** Remove the redundant attribute or change the condition if a different predicate is needed.

---

## IOC068

**Severity:** Info
**Category:** IoCTools.Structural

**Cause:** A class has a manual constructor with injectable parameters but no IoCTools attributes.

**Fix:** Add a lifetime attribute and `[DependsOn<T>]` to opt into IoCTools generator support.

---

## IOC069

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` is used without a lifetime attribute.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` to the class.

---

## IOC070

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** `[DependsOn]` or `[Inject]` is used without a lifetime attribute.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` so the class will be registered and validated.

---

## IOC071

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** `[ConditionalService]` is used without a lifetime attribute.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` to enable registration.

---

## IOC072

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** A hosted service declares a lifetime attribute, but hosted services are registered implicitly.

**Fix:** Remove the lifetime attribute unless the class also exposes additional service interfaces.

---

## IOC074

**Severity:** Info
**Category:** IoCTools.Registration

**Cause:** A class implements multiple interfaces but only has a lifetime attribute without `[RegisterAsAll]`.

**Fix:** Add `[RegisterAsAll]` to register all interfaces automatically.

---

## IOC075

**Severity:** Warning
**Category:** IoCTools.Lifetime

**Cause:** A base class is inherited by IoCTools services with mixed or missing lifetimes.

**Fix:** Place one lifetime attribute on the shared base class so all derived services register consistently.

---

## IOC076

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** A property trivially wraps an IoCTools dependency field without adding behavior.

**Fix:** Access the injected field directly or move the dependency to the base type.

---

## IOC077

**Severity:** Error
**Category:** IoCTools.Dependency

**Cause:** A manually declared field has the same name as an IoCTools-generated dependency field.

**Fix:** Remove the manual field and rely on `[DependsOn]`/`[DependsOnConfiguration]`, or use `[Inject]` with a custom name.

---

## IOC078

**Severity:** Warning
**Category:** IoCTools.Dependency

**Cause:** A MemberNames value collides with an existing field, causing the generated dependency to be skipped.

**Fix:** Remove the existing field or drop MemberNames to let IoCTools generate and wire the dependency.

---

## IOC079

**Severity:** Warning
**Category:** IoCTools.Configuration

**Cause:** A class depends on `IConfiguration` directly instead of using typed configuration.

**Fix:** Use `[DependsOnConfiguration<T>]` or typed options classes instead of raw `IConfiguration`.

---

## IOC080

**Severity:** Error
**Category:** IoCTools.Structural

**Cause:** A class uses IoCTools attributes that require code generation but is not marked as `partial`.

**Fix:** Add `partial` modifier to the class declaration.

---

## IOC081

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** A service is manually registered with the same lifetime that IoCTools already generates.

**Fix:** Remove the manual registration and rely on IoCTools attributes.

---

## IOC082

**Severity:** Error
**Category:** IoCTools.Registration

**Cause:** A service is manually registered with a different lifetime than what IoCTools generates.

**Fix:** Align lifetimes or remove the manual registration.

---

## IOC083

**Severity:** Error
**Category:** IoCTools.Registration

**Cause:** An options type is manually bound via `AddOptions`/`Configure`, but IoCTools already binds it.

**Fix:** Remove the manual binding and rely on generated options registration.

---

## IOC084

**Severity:** Warning
**Category:** IoCTools.Lifetime

**Cause:** A derived class declares the same lifetime attribute already inherited from a base class.

**Fix:** Remove the redundant lifetime attribute or change to a different lifetime if intended.

---

## IOC085

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** A member name matches the generator's default, making the explicit name redundant.

**Fix:** Omit the memberNames value to reduce redundancy.

---

## IOC086

**Severity:** Warning
**Category:** IoCTools.Registration

**Cause:** A service is manually registered but the implementation lacks IoCTools attributes.

**Fix:** Add `[Scoped]`/`[Singleton]`/`[Transient]` (and `[RegisterAs]`) to the implementation instead of manual registration.

---

## IOC087

**Severity:** Error
**Category:** IoCTools.Lifetime

**Cause:** A Transient service depends on a Scoped service. Transient services resolved from the root scope cannot depend on Scoped services.

**Fix:** Change the dependency to `[Singleton]` or `[Transient]`, change this service to `[Scoped]`, inject `IServiceProvider` and call `CreateScope()`, or use a factory delegate.

---

## IOC088

**Severity:** Error
**Category:** IoCTools.Configuration

**Cause:** A configuration type has a circular reference through a property, causing infinite recursion during binding.

**Fix:** Break the cycle by removing the self-referencing property or using a different configuration structure.

---

## IOC089

**Severity:** Warning
**Category:** IoCTools.Configuration

**Cause:** `SupportsReloading=true` is used on a primitive type field, but it only works with Options pattern types.

**Fix:** Remove `SupportsReloading=true` from primitive fields. Use `IOptionsSnapshot<T>` with a complex options type for reloadable configuration.
