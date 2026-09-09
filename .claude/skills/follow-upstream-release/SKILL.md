---
name: follow-upstream-release
description: Use when the user asks to update this repository to a new release of the underlying Microsoft.AspNetCore.Components.Gateway package, or to bump this package's own version to match — including vague prompts like "the upstream released a new version, follow up", "the stock Gateway released a new version, catch up", or "bump to the latest Gateway version".
---

# Following a new Microsoft.AspNetCore.Components.Gateway release

This package (`Toolbelt.Blazor.WebAssembly.ExtensibleGateway`) hosts the stock
`Microsoft.AspNetCore.Components.Gateway` assemblies in-process. It is the .NET 11 counterpart of
`NET10/DevServer`, so this skill mirrors that repository's `follow-upstream-release` skill, adjusted
for prerelease version strings and for the reflection-based invocation this repository relies on.

## Steps

1. Check which versions actually exist before touching anything:
   ```
   curl -s https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.components.gateway/index.json
   ```
   .NET 11 is still prerelease, so versions look like `11.0.0-rc.1.26425.128` — note the trailing
   build number, which changes with every published build even within the same release label
   (`preview.7`, `rc.1`, ...).
2. Update `VersionInfo.props`:
   - `GatewayPackageVersion` — the **full** version string exactly as it appears on nuget.org
     (including the trailing build number), used for the `PackageReference` in
     `ExtensibleGateway/ExtensibleGateway.csproj`.
   - `ExtensibleGatewayVersion` — this repository's own package version, conventionally the release
     label without the trailing build number (e.g. `11.0.0-rc.1`), kept in step with whichever
     release train the stock Gateway just moved to.
3. Update `SampleApp/SampleApp.csproj`'s `Microsoft.AspNetCore.Components.WebAssembly`
   `PackageReference`, which uses a floating wildcard like `11.0.0-rc.1.*` — bump the release-train
   label prefix (`preview.7.*` → `rc.1.*`, etc.), not to one fixed build number. Do the same to the
   commented-out `Microsoft.AspNetCore.Components.Gateway` reference right below it, purely so it
   stays a correct example if someone uncomments it.
4. Update the literal version string in the `README.md` usage example
   (`Toolbelt.Blazor.WebAssembly.ExtensibleGateway` `PackageReference`).
5. Add a new entry at the top of `RELEASE-NOTES.txt` (see the `release-notes` skill for the house
   style), e.g.:
   ```
   v.X.Y.Z
   - Improve: Updated the underlying Microsoft.AspNetCore.Components.Gateway dependency to version X.Y.Z.
   ```
6. Build and test:
   ```
   dotnet pack -c Release
   dotnet test --project ./ExtensibleGateway.Test/ExtensibleGateway.Test.csproj --output detailed
   ```
   Confirm every test passes before considering the bump done. This is not optional here: see the
   compatibility risk below.

## The main compatibility risk: BlazorGatewayInvoker

`ExtensibleGateway/BlazorGatewayInvoker.cs` reflects into an **internal**
`BlazorGateway.BuildWebHost(WebApplicationBuilder)` overload of the stock Gateway assembly (the
public overload only accepts raw `string[]` args and builds its own builder, dropping this
repository's extension service registrations). A stock package version bump is exactly the kind of
change that can silently rename, remove, or change the signature of that internal member. The test
suite is the only thing that actually exercises this reflection call — a full green test run after
bumping is mandatory, not just good practice.

## After this repository is done

The `Toolbelt.Blazor.WebAssembly.ExtensibleGateway.UserSecretsExtension` and `...ImportMapExtension`
repositories (siblings under `NET11/Extensions/`) don't literally depend on the stock Gateway, but
their own package versions are conventionally realigned to match this repository's new
`ExtensibleGatewayVersion` too, purely so users aren't confused by mismatched version numbers across
the package family. Each of those repositories has its own `follow-upstream-release` skill for that
half of the work — run this repository's bump and tests first, then theirs.

The new `ExtensibleGateway` version is typically **not yet published on nuget.org** at this point.
After packing this repository (step 6 above), copy the resulting `.nupkg` from this repo's `_dist/`
folder into each extension repository's `_local-packages/` folder so their tests can restore it
instead of failing to restore — see each extension's `AGENTS.md` for that mechanism.

## Gotchas

- `ExtensibleGateway.Test/ProjectTemplateE2ETests.cs` and `ExtensibleGateway.Test/SampleAppE2ETests.cs`
  use the **floating** `mcr.microsoft.com/dotnet/sdk:11.0` tag (tracks whatever prerelease build MCR
  currently publishes for .NET 11), unlike the NET10 repositories, which pin an exact patch version.
  If a test fails with "You must install or update .NET to run this application," the fix is usually
  `docker pull mcr.microsoft.com/dotnet/sdk:11.0` to refresh the cached image, not editing the tag —
  only change the tag itself once .NET 11 reaches GA and this floating prerelease tag stops being the
  right one to track.
- Files in this repository are stored as LF line endings in git; a normal checkout converts them to
  CRLF locally. Editing with a POSIX tool (`sed`, a bash heredoc) leaves LF, which is harmless but
  worth re-normalizing to CRLF before committing if you want a clean diff.
