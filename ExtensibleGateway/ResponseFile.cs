using System.Text.RegularExpressions;

namespace Toolbelt.Blazor.WebAssembly.ExtensibleGateway;

/// <summary>
/// The INI style file that the MSBuild targets generate for this gateway. It tells the gateway
/// which extension assemblies to load, and which environment variables to set.
/// </summary>
internal class ResponseFile
{
    private const string ExtensionAssembliesSection = "ExtensionAssemblies";

    private const string EnvironmentVariablesSection = "EnvironmentVariables";

    public IReadOnlyList<string> ExtensionAssemblyPaths { get; }

    public IReadOnlyList<KeyValuePair<string, string>> EnvironmentVariables { get; }

    private ResponseFile(IReadOnlyList<string> extensionAssemblyPaths, IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
    {
        this.ExtensionAssemblyPaths = extensionAssemblyPaths;
        this.EnvironmentVariables = environmentVariables;
    }

    public static ResponseFile Load(string? path)
    {
        var sections = ParseSections(path);

        var extensionAssemblyPaths = GetSection(sections, ExtensionAssembliesSection)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var environmentVariables = GetSection(sections, EnvironmentVariablesSection)
            .Select(line => line.IndexOf('=') is var i && i > 0 ? new KeyValuePair<string, string>(line[..i], line[(i + 1)..]) : default)
            .Where(entry => entry.Key is not null)
            .ToArray();

        return new ResponseFile(extensionAssemblyPaths, environmentVariables);
    }

    private static Dictionary<string, List<string>> ParseSections(string? path)
    {
        var sections = new Dictionary<string, List<string>>();
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return sections;

        var currentSection = "";
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#')) continue;

            var matchSection = Regex.Match(trimmedLine, @"^\[(?<name>[^\]]*)\]$");
            if (matchSection.Success)
            {
                currentSection = matchSection.Groups["name"].Value;
                if (!sections.ContainsKey(currentSection)) sections[currentSection] = [];
                continue;
            }

            if (string.IsNullOrEmpty(currentSection)) continue;

            sections[currentSection].Add(trimmedLine);
        }

        return sections;
    }

    private static List<string> GetSection(Dictionary<string, List<string>> sections, string name) => sections.TryGetValue(name, out var lines) ? lines : [];
}
