using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using ExtensibleGateway.Test.Internals;
using Toolbelt;

namespace ExtensibleGateway.Test;

/// <summary>
/// Verifies the extension project template end to end. Inside a disposable Linux container, the
/// template package is installed, an extension project is created from it, the scaffolded middleware
/// is edited to answer a GET request with JSON, and the extension is packed and referenced from the
/// SampleApp. Running the SampleApp then has to serve both the response of the SampleExtension that
/// was already referenced and the response of the extension created here.
/// </summary>
[Parallelizable(ParallelScope.All)]
public class ProjectTemplateE2ETests
{
    private const int ContainerPort = 8080;

    private const string ServerLogPath = "/tmp/sampleapp.log";

    [TestCase("MyExtensibleGatewayExtension")]
    [TestCase("ExtensibleGateway.MyExtension")]
    public async Task ExtensionCreatedFromProjectTemplate_IsLoadedByGateway_InContainer(string extensionName)
    {

        // GIVEN: Copy the solution folder tree into a temporary working folder (excluding bin/obj/.vs folders)
        using var workspace = WorkDirectory.CreateCopyFrom(PathUtils.SolutionDir, entry => entry.Name is not "bin" and not "obj" and not ".vs");
        var distDir = Path.Combine(workspace, "_dist");
        var templatePackageFileName = Path.GetFileName(Directory.GetFiles(distDir, "Toolbelt.Blazor.WebAssembly.ExtensibleGateway.Extension.ProjectTemplates.*.nupkg").Single());

        // GIVEN: Start a container that stays alive, so that the steps below can run in it one by one
        await using var container = new ContainerBuilder("mcr.microsoft.com/dotnet/sdk:11.0-preview")
            .WithBindMount(workspace, "/work")
            .WithWorkingDirectory("/work/SampleApp")
            .WithEntrypoint("tail", "-f", "/dev/null")
            .WithEnvironment("ASPNETCORE_URLS", $"http://0.0.0.0:{ContainerPort}")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithPortBinding(ContainerPort, assignRandomHostPort: true)
            .Build();

        await container.StartAsync();

        // WHEN: Install the project template package and create an extension project from it
        await ExecOrFailAsync(container, "dotnet", "new", "install", $"/work/_dist/{templatePackageFileName}");
        await ExecOrFailAsync(container, "dotnet", "new", "blazorwasmgatewayextension", "-n", extensionName, "-o", $"/work/{extensionName}");

        // WHEN: Edit the scaffolded middleware so that it answers a GET request with JSON
        var safeExtensionName = extensionName.Split('.').Last();
        var startupFilterPath = $"/work/{extensionName}/{safeExtensionName}StartupFilter.cs";
        await ExecOrFailAsync(container, "sed", "-i", "1i using Microsoft.AspNetCore.Http;", startupFilterPath);

        // The placeholder comment in the scaffolded middleware that gets replaced with InjectedMiddlewareCode
        var middlewarePlaceholder = "// You can add custom logic here before the next middleware is invoked";

        // The code to inject into the scaffolded middleware. It must not contain any "#", "&", or backslash characters, because it is spliced in by a "sed" substitution inside the container.
        var messagePath = "/api/message";
        var messageJson = $@"{{""message"":""{Guid.NewGuid()}""}}";
        var injectedMiddlewareCode = $$""""
            if (context.Request.Path == "{{messagePath}}") { context.Response.ContentType = "application/json"; await context.Response.WriteAsync("""{{messageJson}}"""); return; }
            """";

        await ExecOrFailAsync(container, "sed", "-i", $"s#{middlewarePlaceholder}#{injectedMiddlewareCode}#", startupFilterPath);

        await ExecOrFailAsync(container, "grep", "-q", messagePath, startupFilterPath);

        // WHEN: Pack the extension into the local NuGet feed that the sample app restores from
        await ExecOrFailAsync(container, "dotnet", "pack", $"/work/{extensionName}/{extensionName}.csproj", "-c", "Release", "-o", "/work/_dist");

        // WHEN: Add a package reference to the extension that was just created and packed, so that the sample app can load it
        await ExecOrFailAsync(container, "dotnet", "package", "add", extensionName, "--project", "/work/SampleApp/SampleApp.csproj");

        // WHEN: Run the sample app in the background and wait for it to start listening
        await ExecOrFailAsync(container, "sh", "-c", $"dotnet run --no-launch-profile > {ServerLogPath} 2>&1 &");
        var waitResult = await container.ExecAsync(["timeout", "300", "sh", "-c", $"until grep -q 'Now listening on' {ServerLogPath} 2>/dev/null; do sleep 1; done"]);
        var serverLog = (await container.ExecAsync(["cat", ServerLogPath])).Stdout;
        waitResult.ExitCode.Is(0L, message: $"The sample app did not start listening.\n{serverLog}");

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(ContainerPort)}")
        };

        // THEN: Access the sample application's endpoints to verify their behavior
        // 1. Access the root URL and verify that the Blazor WASM HTML is returned
        var indexResponse = await httpClient.GetAsync("/");
        indexResponse.StatusCode.Is(HttpStatusCode.OK);
        indexResponse.Content.Headers.ContentType.IsNotNull().MediaType.Is("text/html");

        // 2. Access the SampleExtension endpoint and verify that it still answers
        var helloResponse = await httpClient.GetAsync("/api/helloworld");
        helloResponse.StatusCode.Is(HttpStatusCode.OK);
        (await helloResponse.Content.ReadAsStringAsync()).Is("{\"message\":\"Hello, World!\"}");

        // 3. Access the endpoint of the extension created from the project template
        var greetingResponse = await httpClient.GetAsync(messagePath);
        greetingResponse.StatusCode.Is(HttpStatusCode.OK);
        (await greetingResponse.Content.ReadAsStringAsync()).Is(messageJson);
    }

    private static async Task<string> ExecOrFailAsync(IContainer container, params string[] command)
    {
        var result = await container.ExecAsync(command);
        result.ExitCode.Is(0L, message: $"\"{string.Join(' ', command)}\" failed in the container (exit code {result.ExitCode}).\n{result.Stdout}\n{result.Stderr}");
        return result.Stdout;
    }
}
