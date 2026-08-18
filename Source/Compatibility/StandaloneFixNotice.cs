using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace FloatMenuRevalidationControl.Compatibility
{
    [StaticConstructorOnStartup]
    internal static class StandaloneFixNotice
    {
        private sealed class StandaloneFixInfo
        {
            internal string ModuleKey;
            internal string PackageId;
            internal string DisplayName;
            internal string AssemblyName;
        }

        private static readonly StandaloneFixInfo[] StandaloneFixes =
        {
            new StandaloneFixInfo
            {
                ModuleKey = "RigorMortis",
                PackageId = "vorsel.rigormortis.floatmenufix",
                DisplayName = "MoeLotl: Rigor Mortis - Float Menu Fix",
                AssemblyName = "RigorMortisFloatMenuFix"
            },
            new StandaloneFixInfo
            {
                ModuleKey = "MiliraRace",
                PackageId = "vorsel.milirarace.floatmenufix",
                DisplayName = "Milira Race - Float Menu Fix",
                AssemblyName = "MiliraRaceFloatMenuFix"
            },
            new StandaloneFixInfo
            {
                ModuleKey = "WingsOfDemocracy",
                PackageId = "vorsel.milirawingsofdemocracy.floatmenufix",
                DisplayName = "Milira: Wings of Democracy - Float Menu Fix",
                AssemblyName = "MiliraWingsOfDemocracyFloatMenuFix"
            }
        };

        static StandaloneFixNotice()
        {
            Dictionary<string, bool> policies = LoadPolicies();
            for (int index = 0; index < StandaloneFixes.Length; index++)
            {
                StandaloneFixInfo fix = StandaloneFixes[index];
                if (!ModsConfig.IsActive(fix.PackageId)
                    || IsAssemblyLoaded(fix.AssemblyName))
                {
                    continue;
                }

                bool enabled;
                policies.TryGetValue(fix.ModuleKey, out enabled);
                if (enabled)
                {
                    Log.Warning(
                        "[FMPO] Standalone Fix '" + fix.DisplayName
                        + "' was not loaded because its functionality is included "
                        + "in Float Menu Performance Optimizer. You may remove the "
                        + "standalone Fix from the mod list.");
                }
                else
                {
                    Log.Warning(
                        "[FMPO] Standalone Fix '" + fix.DisplayName
                        + "' was not loaded because this compatibility fix is "
                        + "disabled by the optimizer's XML policy. You may remove "
                        + "the standalone Fix from the mod list.");
                }
            }
        }

        private static Dictionary<string, bool> LoadPolicies()
        {
            Dictionary<string, bool> policies =
                new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            List<FloatMenuCompatibilityDef> definitions =
                DefDatabase<FloatMenuCompatibilityDef>.AllDefsListForReading;
            for (int index = 0; index < definitions.Count; index++)
            {
                FloatMenuCompatibilityDef definition = definitions[index];
                if (definition != null && !string.IsNullOrEmpty(definition.moduleKey))
                {
                    policies[definition.moduleKey] = definition.enabled;
                }
            }

            return policies;
        }

        private static bool IsAssemblyLoaded(string assemblyName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                if (string.Equals(
                    assemblies[index].GetName().Name,
                    assemblyName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
