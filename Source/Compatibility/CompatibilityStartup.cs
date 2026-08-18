using Verse;

namespace FloatMenuRevalidationControl.Compatibility
{
    [StaticConstructorOnStartup]
    internal static class CompatibilityStartup
    {
        static CompatibilityStartup()
        {
            CompatibilityManager.Initialize();
        }
    }
}
