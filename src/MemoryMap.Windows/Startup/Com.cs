using System.Runtime.InteropServices.Marshalling;

namespace MemoryMap.Windows.Startup;

/// <summary>The one ComWrappers the Shell readers wrap COM pointers with (D042).</summary>
internal static class Com
{
    public static readonly StrategyBasedComWrappers Wrappers = new();
}
