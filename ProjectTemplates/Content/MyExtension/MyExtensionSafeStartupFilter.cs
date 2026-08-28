using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace MyExtension;

public class MyExtensionSafeStartupFilter : IStartupFilter
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
