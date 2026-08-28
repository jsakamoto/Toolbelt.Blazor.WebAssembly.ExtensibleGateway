using System.Text.RegularExpressions;
using ExtensibleGateway.Test.Internals;
using Toolbelt;
using Toolbelt.Diagnostics;

namespace ExtensibleGateway.Test;

/// <summary>
/// Runs the host's "dotnet pack" as-is via XProcess to verify that ExtensibleGateway / SampleExtension
/// can be generated as NuGet packages. To avoid polluting the host's Gateway source tree,
/// the pack is performed inside a temporary folder duplicated by WorkDirectory.CreateCopyFrom.
/// </summary>
[Parallelizable(ParallelScope.All)]
public class PackageBuildTests
{
    [TestCase("ExtensibleGateway/ExtensibleGateway.csproj", "Toolbelt.Blazor.WebAssembly.ExtensibleGateway")]
    [TestCase("SampleExtension/SampleExtension.csproj", "Toolbelt.Blazor.WebAssembly.ExtensibleGateway.SampleExtension")]
    [TestCase("ProjectTemplates/ProjectTemplates.msbuild", "Toolbelt.Blazor.WebAssembly.ExtensibleGateway.Extension.ProjectTemplates")]
    public async Task CanPackProjectAsNuGetPackage(string projectRelativePath, string packageId)
    {
        // GIVEN: Copy the solution folder tree into a temporary working folder (excluding bin/obj/_dist/.vs folders)
        using var workDir = WorkDirectory.CreateCopyFrom(PathUtils.SolutionDir, entry => entry.Name is not "bin" and not "obj" and not "_dist" and not ".vs");
        var projectPath = Path.Combine([workDir, .. projectRelativePath.Split('/')]);
        var distDir = Path.Combine(workDir, "_dist");
        Directory.CreateDirectory(distDir);
        Directory.GetFiles(distDir).Any().IsFalse(); // Ensure that the _dist folder is empty before running dotnet pack

        // WHEN: Run dotnet pack
        using var process = await XProcess
            .Start("dotnet", $"pack \"{projectPath}\" -c Release")
            .WaitForExitAsync();
        process.ExitCode.Is(0, process.Output);

        // THEN: Verify that a .nupkg file was generated in the _dist folder
        var nupkgNamePattern = new Regex($@"^{Regex.Escape(packageId)}\.\d.*\.nupkg$");
        Directory.GetFiles(distDir)
            .Any(f => nupkgNamePattern.IsMatch(Path.GetFileName(f)))
            .IsTrue(message: $"No .nupkg file matching \"{packageId}\" was produced in {distDir}");
    }
}
