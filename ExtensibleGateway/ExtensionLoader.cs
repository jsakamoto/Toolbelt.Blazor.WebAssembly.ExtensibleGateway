using System.Reflection;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Hosting;

namespace Toolbelt.Blazor.WebAssembly.ExtensibleGateway;

/// <summary>
/// Loads extension assemblies and runs their hosting startups.
/// </summary>
/// <remarks>
/// The stock gateway is built on <see cref="WebApplication.CreateSlimBuilder(string[])"/>, and the
/// slim builder does not implement the hosting startup mechanism at all. That is why this gateway
/// discovers and runs <see cref="IHostingStartup"/> implementations on its own instead of relying
/// on the ASPNETCORE_HOSTINGSTARTUPASSEMBLIES environment variable.
/// </remarks>
internal static class ExtensionLoader
{
    /// <summary>
    /// Loads the given extension assemblies into the default load context. Assemblies that an
    /// extension carries privately are resolved by probing the folder the extension lives in, so
    /// extension packages do not have to ship a hand written ".deps.json".
    /// </summary>
    public static IReadOnlyList<Assembly> Load(IReadOnlyList<string> assemblyPaths)
    {
        if (assemblyPaths.Count == 0) return [];

        var probingDirectories = assemblyPaths
            .Select(Path.GetDirectoryName)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        AssemblyLoadContext.Default.Resolving += (context, assemblyName) => Resolve(context, assemblyName, probingDirectories);

        var assemblies = new List<Assembly>();
        foreach (var assemblyPath in assemblyPaths)
        {
            if (!File.Exists(assemblyPath))
            {
                Warn($"The extension assembly \"{assemblyPath}\" was not found, so it was skipped.");
                continue;
            }

            try
            {
                assemblies.Add(AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath));
            }
            catch (Exception exception)
            {
                Warn($"The extension assembly \"{assemblyPath}\" could not be loaded, so it was skipped. {exception.Message}");
            }
        }

        return assemblies;
    }

    /// <summary>
    /// Runs the <see cref="IHostingStartup"/> implementations that the given assemblies declare.
    /// </summary>
    /// <remarks>
    /// The builder that extensions receive is <see cref="WebApplicationBuilder.WebHost"/>. It
    /// supports "ConfigureServices", "ConfigureAppConfiguration", "GetSetting" and "UseSetting",
    /// which is everything an extension needs to register an <c>IStartupFilter</c>. Note that the
    /// startup filters themselves must not be invoked by hand: the web host applies every
    /// registered filter when the application starts, and applying them again would add the same
    /// middleware to the pipeline twice.
    /// </remarks>
    public static void RunHostingStartups(IReadOnlyList<Assembly> assemblies, WebApplicationBuilder builder)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var attribute in assembly.GetCustomAttributes<HostingStartupAttribute>())
            {
                try
                {
                    var hostingStartup = (IHostingStartup?)Activator.CreateInstance(attribute.HostingStartupType);
                    hostingStartup?.Configure(builder.WebHost);
                }
                catch (Exception exception)
                {
                    Warn($"The hosting startup \"{attribute.HostingStartupType.FullName}\" in \"{assembly.GetName().Name}\" threw an exception, so it was skipped. {exception.Message}");
                }
            }
        }
    }

    private static Assembly? Resolve(AssemblyLoadContext context, AssemblyName assemblyName, IReadOnlyList<string> probingDirectories)
    {
        if (assemblyName.Name is null) return null;

        foreach (var directory in probingDirectories)
        {
            var candidate = Path.Combine(directory, assemblyName.Name + ".dll");
            if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
        }

        return null;
    }

    private static void Warn(string message) => Console.Error.WriteLine($"warning: {message}");
}
