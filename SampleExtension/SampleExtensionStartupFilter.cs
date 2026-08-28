using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Toolbelt.Blazor.WebAssembly.ExtensibleGateway.SampleExtension;

public class SampleExtensionStartupFilter : IStartupFilter
{
    private static readonly PathString _HelloWorldPath = new("/api/helloworld");

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                var request = context.Request;
                if (HttpMethods.IsGet(request.Method) && request.Path.Equals(_HelloWorldPath, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"message\":\"Hello, World!\"}");
                    return;
                }

                await nextMiddleware();
            });

            next(app);
        };
    }
}
