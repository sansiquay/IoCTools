#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "usage: $0 <version>" >&2
  exit 2
fi

version="$1"
root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

require_file() {
  local file="$1"
  [[ -f "$root_dir/$file" ]] || fail "missing $file"
}

require_contains() {
  local file="$1"
  local needle="$2"
  grep -Fq -- "$needle" "$root_dir/$file" || fail "$file missing: $needle"
}

require_not_contains() {
  local file="$1"
  local needle="$2"
  if grep -Fq -- "$needle" "$root_dir/$file"; then
    fail "$file still contains: $needle"
  fi
}

require_file .github/workflows/ci-main.yml
require_file .github/workflows/release.yml

for project in \
  IoCTools.Abstractions/IoCTools.Abstractions.csproj \
  IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj \
  IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj \
  IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj \
  IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj; do
  require_file "$project"
done

require_contains IoCTools.Abstractions/IoCTools.Abstractions.csproj "<Version>$version</Version>"
require_contains IoCTools.Abstractions/IoCTools.Abstractions.csproj "<PackageVersion>$version</PackageVersion>"
require_contains IoCTools.Abstractions/IoCTools.Abstractions.csproj "<AssemblyVersion>$version.0</AssemblyVersion>"
require_contains IoCTools.Abstractions/IoCTools.Abstractions.csproj "<FileVersion>$version.0</FileVersion>"
require_contains IoCTools.Abstractions/IoCTools.Abstractions.csproj "https://github.com/sansiquay/IoCTools"

require_contains IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj "<Version>$version</Version>"
require_contains IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj "<PackageVersion>$version</PackageVersion>"
require_contains IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj "<AssemblyVersion>$version.0</AssemblyVersion>"
require_contains IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj "<FileVersion>$version.0</FileVersion>"
require_contains IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj "https://github.com/sansiquay/IoCTools"

require_contains IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj "<Version>$version</Version>"
require_contains IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj "<PackageVersion>$version</PackageVersion>"
require_contains IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj "<AssemblyVersion>$version.0</AssemblyVersion>"
require_contains IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj "<FileVersion>$version.0</FileVersion>"
require_contains IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj "https://github.com/sansiquay/IoCTools"

require_contains IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj "<Version>$version</Version>"
require_contains IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj "<PackageVersion>$version</PackageVersion>"
require_contains IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj "<AssemblyVersion>$version.0</AssemblyVersion>"
require_contains IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj "<FileVersion>$version.0</FileVersion>"
require_contains IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj "https://github.com/sansiquay/IoCTools"

require_contains IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj "<Version>$version</Version>"
require_contains IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj "<PackageVersion>$version</PackageVersion>"
require_contains IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj "<AssemblyVersion>$version.0</AssemblyVersion>"
require_contains IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj "<FileVersion>$version.0</FileVersion>"
require_contains IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj "https://github.com/sansiquay/IoCTools"

require_contains .github/workflows/ci-main.yml "name: CI Main Branch"
require_contains .github/workflows/ci-main.yml "build-and-test:"
require_not_contains .github/workflows/ci-main.yml "publish NuGet"
require_not_contains .github/workflows/ci-main.yml "alirezanet/publish-nuget"
require_not_contains .github/workflows/ci-main.yml "dotnet nuget push"

require_contains .github/workflows/release.yml "name: Release Packages"
require_contains .github/workflows/release.yml "push:"
require_contains .github/workflows/release.yml "tags:"
require_contains .github/workflows/release.yml "- 'v*'"
require_contains .github/workflows/release.yml "build-test-pack:"
require_contains .github/workflows/release.yml "publish:"
require_contains .github/workflows/release.yml "IoCTools.Abstractions/IoCTools.Abstractions.csproj"
require_contains .github/workflows/release.yml "IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj"
require_contains .github/workflows/release.yml "IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj"
require_contains .github/workflows/release.yml "IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj"
require_contains .github/workflows/release.yml "IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj"
require_contains .github/workflows/release.yml "dotnet nuget push"

echo "PASS: release surface is aligned for $version"
