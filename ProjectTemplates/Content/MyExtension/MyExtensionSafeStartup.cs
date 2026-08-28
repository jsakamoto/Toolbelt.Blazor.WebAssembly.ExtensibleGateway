using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MyExtension;

[assembly: HostingStartup(typeof(MyExtensionSafeStartup))]

namespace MyExtension;

public class MyExtensionSafeStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Register the startup filter
            services.AddSingleton<IStartupFilter, MyExtensionSafeStartupFilter>();

            // You can register other services here as needed
        });
    }
}
