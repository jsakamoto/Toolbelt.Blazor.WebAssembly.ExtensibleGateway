# Blazor WebAssembly Extensible Gateway

[![unit tests](https://github.com/jsakamoto/Toolbelt.Blazor.WebAssembly.ExtensibleGateway/actions/workflows/unit-tests.yml/badge.svg)](https://github.com/jsakamoto/Toolbelt.Blazor.WebAssembly.ExtensibleGateway/actions/workflows/unit-tests.yml) [![NuGet Package](https://img.shields.io/nuget/v/Toolbelt.Blazor.WebAssembly.ExtensibleGateway.svg)](https://www.nuget.org/packages/Toolbelt.Blazor.WebAssembly.ExtensibleGateway/) [![Discord](https://img.shields.io/discord/798312431893348414?style=flat&logo=discord&logoColor=white&label=Blazor%20Community&labelColor=5865f2&color=gray)](https://discord.com/channels/798312431893348414/1202165955900473375)

An alternative Blazor WebAssembly gateway server that can be extended with additional NuGet packages for custom middleware.

## What is this?

In a standalone Blazor WebAssembly project on .NET 11, the development server is provided by the `Microsoft.AspNetCore.Components.Gateway` NuGet package. This package works well out of the box, but it does not offer any way to customize or extend the gateway's behavior.

**Toolbelt.Blazor.WebAssembly.ExtensibleGateway** is a drop-in replacement for that default gateway. It runs the same gateway inside, so replacing the default package with this one does not change any behavior by itself. Your project will continue to work exactly as before.

The key difference is that this gateway is **extensible**. An extension package ships an `IHostingStartup` class, and this gateway finds it and runs it at startup. That lets the extension register its own services and middleware. You can install extension packages next to this one to add new features to your development experience.

### Examples of possible extensions

- **User Secrets integration**. Merge .NET User Secrets into the `appsettings.json` response served to the client, so secret configuration values are available during development without checking them into source control.
- **CSP hash rewriting**. Automatically update Content Security Policy hash values in `index.html` to match the actual content, eliminating manual hash maintenance during development.

## How to use

### 1. Replace the default gateway package

Remove the default gateway package from your Blazor WebAssembly project and add this one instead.

**Using the .NET CLI**

```shell
dotnet remove package Microsoft.AspNetCore.Components.Gateway
dotnet add package Toolbelt.Blazor.WebAssembly.ExtensibleGateway
```

**Or, edit your project file (`.csproj`) directly**

Find the following `PackageReference` in your `.csproj` file.

```xml
<PackageReference Include="Microsoft.AspNetCore.Components.Gateway" Version="..." PrivateAssets="all" />
```

Then replace it with this one.

```xml
<PackageReference Include="Toolbelt.Blazor.WebAssembly.ExtensibleGateway" Version="11.0.0-preview.7" PrivateAssets="all" />
```

### 2. Install extension packages

Then, add any extension packages you need. For example,

```shell
dotnet add package <ExtensionPackageName>
```

That's it. No additional code or configuration is required. The extensions are loaded automatically when you run your project.

## Creating your own extension

An extension is a NuGet package that ships an `IHostingStartup` implementation plus a `.targets` file that tells this gateway where to find it.

This gateway reads the `[assembly: HostingStartup(...)]` attribute of each extension assembly and runs the startup class by itself.

### 1. Create the project from the template

Install the extension project template.

```shell
dotnet new install Toolbelt.Blazor.WebAssembly.ExtensibleGateway.Extension.ProjectTemplates
```

Then create your project.

**Using the .NET CLI**

```shell
dotnet new blazorwasmgatewayextension -n {YourExtensionName}
```

**Or, using Visual Studio or VS Code with C# Dev Kit**

Pick "Blazor WebAssembly Extensible Gateway Extension" from the new project templates.

The generated project already has the project file and the `.targets` file that this gateway needs. If you want to build the project by hand instead, see "What the template sets up" below.

### 2. Implement your middleware

Open the generated `{YourExtensionName}StartupFilter.cs` file. It holds a small middleware that does nothing at first. Change it to do what you need.

The code below shows the whole shape of an extension. An `IStartupFilter` adds your middleware to the pipeline, and an `IHostingStartup` registers that filter.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using YourExtensionName;

[assembly: HostingStartup(typeof(YourExtensionNameStartup))]

namespace YourExtensionName;

public class YourExtensionNameStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Register the startup filter
            services.AddSingleton<IStartupFilter, YourExtensionNameStartupFilter>();

            // You can register other services here as needed
        });
    }
}

public class YourExtensionNameStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        // Return a registration action that adds middleware to the pipeline
        return app =>
        {
            // Example middleware that does nothing but call the next middleware
            app.Use(async (context, nextMiddleware) =>
            {
                // You can add custom logic here before the next middleware is invoked

                await nextMiddleware();

                // You can add custom logic here after the next middleware has completed
            });

            // Call the next startup filter in the chain
            next(app);
        };
    }
}
```

Do not call `IStartupFilter.Configure` yourself anywhere. The web host applies every registered filter when the application starts. If you apply them again by hand, the same middleware goes into the pipeline twice.

### 3. Build the NuGet package

```shell
dotnet pack -c Release
```

### 4. Use your extension

Install your generated NuGet package into a Blazor WebAssembly standalone project alongside `Toolbelt.Blazor.WebAssembly.ExtensibleGateway`. When you run the project, your custom middleware will be automatically loaded into the gateway's HTTP request pipeline.

## What the template sets up

You do not need to read this part if you use the project template. It is here for people who want to build an extension project by hand, or who want to know what the template does.

### The project file

The gateway loads your extension from the `tools/net11.0` folder of your package, not from `lib`. Your project file has to put the build output there.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <NoWarn>$(NoWarn);NU5128</NoWarn>
    <PackageId>YourExtensionName</PackageId>
    <DevelopmentDependency>true</DevelopmentDependency>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <!-- Your extension is loaded by path, and its dependencies are found by looking into the
         folder it lives in, so every dependency has to sit next to your extension assembly. -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <!-- PrivateAssets keeps this out of the "frameworkReferences" section of the .nuspec. -->
    <FrameworkReference Include="Microsoft.AspNetCore.App" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <None Include="PackageContents/**/*" Pack="true" PackagePath="/%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>

  <Target Name="BundleAssemblies" BeforeTargets="GenerateNuspec;_GetPackageFiles" AfterTargets="Build">
    <ItemGroup>
      <None Include="$(TargetDir)/*.dll" Pack="true" PackagePath="/tools/$(TargetFramework)" />
    </ItemGroup>
  </Target>

</Project>
```

Your extension does not need to ship a `.deps.json` file. The gateway loads your extension assembly by its path, and it looks for the extension's own dependencies in the same folder.

### The .targets file

Ship a `.targets` file in the `build` folder of your package that tells the gateway where your assembly is. With the project file above, put the source of that file at `PackageContents/build/YourExtensionName.targets`. The file name must match your package id, otherwise NuGet will not import it.

```xml
<Project>

  <Target Name="BzExGateway_YourExtensionName_PrepareResponseFile" BeforeTargets="BzExGateway_GenerateResponseFile">
    <ItemGroup>
      <BzExGatewayAssemblyPath Include="$(MSBuildThisFileDirectory)../tools/net11.0/YourExtensionName.dll" />
    </ItemGroup>
  </Target>

</Project>
```

The following item groups are available.

| Item group | Purpose |
|---|---|
| `BzExGatewayAssemblyPath` | Paths of the extension assemblies to load |
| `BzExGatewayEnvValue` | Extra environment variables, in `NAME=VALUE` form |

`BzExGatewayEnvValue` sets the variable inside the gateway process at startup. It cannot set variables that the .NET host reads when the process starts, such as `DOTNET_ADDITIONAL_DEPS`.

## License and 3rd Party Notices

This project is licensed under the Mozilla Public License v2.0. See the [LICENSE](https://github.com/jsakamoto/Toolbelt.Blazor.WebAssembly.ExtensibleGateway/blob/main/LICENSE) file for details.

This project includes third-party components. See the [THIRD-PARTY-NOTICES](https://github.com/jsakamoto/Toolbelt.Blazor.WebAssembly.ExtensibleGateway/blob/main/THIRD-PARTY-NOTICES.txt) file for details.
