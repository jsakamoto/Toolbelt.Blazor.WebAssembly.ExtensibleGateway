using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Components.Gateway;

namespace Toolbelt.Blazor.WebAssembly.ExtensibleGateway;

/// <summary>
/// Hands a prepared <see cref="WebApplicationBuilder"/> over to the stock Blazor gateway.
/// </summary>
/// <remarks>
/// <see cref="BlazorGateway"/> exposes two overloads of "BuildWebHost". The public one takes the
/// command line arguments, creates a slim builder of its own, and passes it to the internal one.
/// Only the public overload decides which builder to use, so calling the internal overload lets
/// this gateway supply a builder that already carries the extensions' service registrations.
/// </remarks>
internal static class BlazorGatewayInvoker
{
    private const string MethodName = "BuildWebHost";

    public static WebApplication BuildWebHost(WebApplicationBuilder builder)
    {
        var method = typeof(BlazorGateway).GetMethod(MethodName, BindingFlags.Static | BindingFlags.NonPublic, [typeof(WebApplicationBuilder)])
            ?? throw new InvalidOperationException(BuildIncompatibilityMessage());

        try
        {
            return (WebApplication)method.Invoke(null, [builder])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static string BuildIncompatibilityMessage()
    {
        var gatewayVersion = typeof(BlazorGateway).Assembly.GetName().Version;
        return
            $"Could not find the \"{typeof(BlazorGateway).FullName}.{MethodName}(WebApplicationBuilder)\" method " +
            $"in the Blazor gateway assembly (version {gatewayVersion}). " +
            "This version of Toolbelt.Blazor.WebAssembly.ExtensibleGateway is not compatible with that gateway. " +
            "Please update Toolbelt.Blazor.WebAssembly.ExtensibleGateway to a version that matches your .NET SDK.";
    }
}
