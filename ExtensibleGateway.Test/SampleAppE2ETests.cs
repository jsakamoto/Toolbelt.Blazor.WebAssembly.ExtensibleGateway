using System.Net;
using DotNet.Testcontainers.Builders;
using ExtensibleGateway.Test.Internals;
using Toolbelt;

namespace ExtensibleGateway.Test;

/// <summary>
/// Packages ExtensibleGateway / SampleExtension as NuGet packages, runs the SampleApp that
/// references them via "dotnet run" inside a disposable Linux container, and verifies over HTTP
/// that the Blazor WASM Gateway actually works. This is an E2E test.
///
/// Because the container has a pristine NuGet global package cache, there is no risk of
/// picking up stale content from the host's cache for the same package ID/version.
/// </summary>
[Parallelizable(ParallelScope.All)]
public class SampleAppE2ETests
{
    private const int ContainerPort = 8080;

    [Test]
    public async Task SampleAppServesContentAndExtensionEndpoint_ViaGatewayInContainer()
    {
        // GIVEN: Copy the solution folder tree into a temporary working folder (excluding bin/obj/.vs folders)
        using var workspace = WorkDirectory.CreateCopyFrom(PathUtils.SolutionDir, entry => entry.Name is not "bin" and not "obj" and not ".vs");

        // WHEN: Run the sample program with dotnet run inside the container
        await using var container = new ContainerBuilder("mcr.microsoft.com/dotnet/sdk:11.0-preview")
            .WithBindMount(workspace, "/work")
            .WithWorkingDirectory("/work/SampleApp")
            .WithEntrypoint("dotnet", "run", "--no-launch-profile")
            .WithEnvironment("ASPNETCORE_URLS", $"http://0.0.0.0:{ContainerPort}")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithPortBinding(ContainerPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Now listening on"))
            .Build();

        await container.StartAsync();

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(ContainerPort)}")
        };

        // THEN: Access the sample application's endpoints to verify their behavior
        // 1. Access the root URL and verify that the Blazor WASM HTML is returned
        var indexResponse = await httpClient.GetAsync("/");
        indexResponse.StatusCode.Is(HttpStatusCode.OK);
        indexResponse.Content.Headers.ContentType.IsNotNull().MediaType.Is("text/html");

        // 2. Access the SampleExtension endpoint and verify that JSON is returned
        var helloResponse = await httpClient.GetAsync("/api/helloworld");
        helloResponse.StatusCode.Is(HttpStatusCode.OK);
        (await helloResponse.Content.ReadAsStringAsync()).Is("{\"message\":\"Hello, World!\"}");

        // 3. Verify that the Blazor WASM bootstrap JS is returned
        var blazorBootResponse = await httpClient.GetAsync("/_framework/blazor.webassembly.js");
        blazorBootResponse.StatusCode.Is(HttpStatusCode.OK);
    }
}
