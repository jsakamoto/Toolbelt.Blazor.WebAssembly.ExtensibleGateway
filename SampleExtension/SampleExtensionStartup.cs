using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Toolbelt.Blazor.WebAssembly.ExtensibleGateway.SampleExtension;

[assembly: HostingStartup(typeof(SampleExtensionStartup))]

namespace Toolbelt.Blazor.WebAssembly.ExtensibleGateway.SampleExtension;

public class SampleExtensionStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, SampleExtensionStartupFilter>();
        });
    }
}
