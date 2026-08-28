using CommandLineSwitchParser;
using Toolbelt.Blazor.WebAssembly.ExtensibleGateway;

// Arguments before "--" belong to this host, arguments after it belong to the stock gateway.
var hostArgs = args.TakeWhile(arg => arg != "--").ToArray();
var gatewayArgs = args.SkipWhile(arg => arg != "--").Skip(1).ToArray();
var options = CommandLineSwitch.Parse<CommandLineOptions>(ref hostArgs);

var responseFile = ResponseFile.Load(options.ResponseFilePath);

foreach (var (name, value) in responseFile.EnvironmentVariables)
{
    Environment.SetEnvironmentVariable(name, value);
}

var extensions = ExtensionLoader.Load(responseFile.ExtensionAssemblyPaths);

// The stock gateway resolves the static assets it ships with relative to the content root, and
// this host sits in the same folder as those assets.
var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
{
    Args = gatewayArgs,
    ContentRootPath = AppContext.BaseDirectory,
});

ExtensionLoader.RunHostingStartups(extensions, builder);

var app = BlazorGatewayInvoker.BuildWebHost(builder);

app.Run();
