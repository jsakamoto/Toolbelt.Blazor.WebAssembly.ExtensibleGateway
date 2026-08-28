using Toolbelt;

namespace ExtensibleGateway.Test.Internals;

internal static class PathUtils
{
    public static readonly string SolutionDir = FileIO.FindContainerDirToAncestor("*.slnx");
}
