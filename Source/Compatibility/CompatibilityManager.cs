using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace FloatMenuRevalidationControl.Compatibility
{
    public sealed class FloatMenuCompatibilityDef : Def
    {
        public string moduleKey;
        public string targetPackageId;
        public string legacyAssemblyName;
        public string settingLabelKey;
        public bool enabled = true;
    }

    internal static class CompatibilityManager
    {
        private const string ControllerHarmonyId =
            "vorsel.floatmenuperformanceoptimizer.compatibility.controller";

        private sealed class ModuleRegistration
        {
            internal Func<bool> Install;
            internal Action Uninstall;
            internal Action DisableLegacyPatches;
        }

        private static bool initialized;

        private static readonly Dictionary<string, ModuleRegistration> Modules =
            new Dictionary<string, ModuleRegistration>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "RigorMortis",
                    new ModuleRegistration
                    {
                        Install = RigorMortis.RigorMortisCompatibilityModule.Install,
                        Uninstall = RigorMortis.RigorMortisCompatibilityModule.Uninstall,
                        DisableLegacyPatches =
                            RigorMortis.RigorMortisCompatibilityModule.DisableLegacyPatches
                    }
                },
                {
                    "MiliraRace",
                    new ModuleRegistration
                    {
                        Install = MiliraRace.MiliraRaceCompatibilityModule.Install,
                        Uninstall = MiliraRace.MiliraRaceCompatibilityModule.Uninstall,
                        DisableLegacyPatches =
                            MiliraRace.MiliraRaceCompatibilityModule.DisableLegacyPatches
                    }
                },
                {
                    "WingsOfDemocracy",
                    new ModuleRegistration
                    {
                        Install = WingsOfDemocracy.WingsOfDemocracyCompatibilityModule.Install,
                        Uninstall = WingsOfDemocracy.WingsOfDemocracyCompatibilityModule.Uninstall,
                        DisableLegacyPatches =
                            WingsOfDemocracy.WingsOfDemocracyCompatibilityModule.DisableLegacyPatches
                    }
                }
            };

        internal static void Initialize()
        {
            List<FloatMenuCompatibilityDef> definitions =
                DefDatabase<FloatMenuCompatibilityDef>.AllDefsListForReading;
            if (definitions == null || definitions.Count == 0)
            {
                Log.Warning(
                    "[FMPO] No compatibility definitions were loaded. "
                    + "Targeted compatibility optimizations are inactive.");
                return;
            }

            initialized = true;

            for (int index = 0; index < definitions.Count; index++)
            {
                ApplyDefinition(definitions[index]);
            }
        }

        internal static List<FloatMenuCompatibilityDef> GetVisibleDefinitions()
        {
            List<FloatMenuCompatibilityDef> visible =
                new List<FloatMenuCompatibilityDef>();
            List<FloatMenuCompatibilityDef> definitions =
                DefDatabase<FloatMenuCompatibilityDef>.AllDefsListForReading;
            if (definitions == null)
            {
                return visible;
            }

            for (int index = 0; index < definitions.Count; index++)
            {
                FloatMenuCompatibilityDef definition = definitions[index];
                if (definition != null
                    && definition.enabled
                    && !string.IsNullOrEmpty(definition.targetPackageId)
                    && ModsConfig.IsActive(definition.targetPackageId))
                {
                    visible.Add(definition);
                }
            }

            return visible;
        }

        internal static bool IsLegacyStandaloneLoaded(
            FloatMenuCompatibilityDef definition)
        {
            return definition != null
                && IsAssemblyLoaded(definition.legacyAssemblyName);
        }

        internal static void ApplyUserSetting(string moduleKey)
        {
            if (!initialized)
            {
                return;
            }

            FloatMenuCompatibilityDef definition = FindDefinition(moduleKey);
            if (definition != null)
            {
                ApplyDefinition(definition);
                CloseOpenFloatMenu();
            }
        }

        internal static void ApplyAllUserSettings()
        {
            if (!initialized)
            {
                return;
            }

            List<FloatMenuCompatibilityDef> definitions =
                DefDatabase<FloatMenuCompatibilityDef>.AllDefsListForReading;
            if (definitions == null)
            {
                return;
            }

            for (int index = 0; index < definitions.Count; index++)
            {
                ApplyDefinition(definitions[index]);
            }

            CloseOpenFloatMenu();
        }

        private static FloatMenuCompatibilityDef FindDefinition(string moduleKey)
        {
            List<FloatMenuCompatibilityDef> definitions =
                DefDatabase<FloatMenuCompatibilityDef>.AllDefsListForReading;
            if (definitions == null)
            {
                return null;
            }

            for (int index = 0; index < definitions.Count; index++)
            {
                FloatMenuCompatibilityDef definition = definitions[index];
                if (definition != null
                    && string.Equals(
                        definition.moduleKey,
                        moduleKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }

            return null;
        }

        private static void ApplyDefinition(FloatMenuCompatibilityDef definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.moduleKey))
            {
                return;
            }

            ModuleRegistration module;
            if (!Modules.TryGetValue(definition.moduleKey, out module))
            {
                Log.Warning(
                    "[FMPO] Unknown compatibility module in XML: "
                    + definition.moduleKey);
                return;
            }

            if (!string.IsNullOrEmpty(definition.targetPackageId)
                && !ModsConfig.IsActive(definition.targetPackageId))
            {
                module.Uninstall();
                return;
            }

            if (!definition.enabled)
            {
                Log.Message(
                    "[FMPO] Compatibility module " + definition.moduleKey
                    + " is disabled by XML policy.");
                module.Uninstall();
                LongEventHandler.ExecuteWhenFinished(module.DisableLegacyPatches);
                return;
            }

            if (IsAssemblyLoaded(definition.legacyAssemblyName))
            {
                module.Uninstall();
                Log.Message(
                    "[FMPO] Compatibility module " + definition.moduleKey
                    + " is supplied by a loaded standalone Fix; the integrated "
                    + "copy will remain inactive.");
                return;
            }

            FloatMenuRevalidationSettings settings =
                FloatMenuRevalidationControlMod.CurrentSettings;
            if (settings != null
                && !settings.GetCompatibilityFixEnabled(definition.moduleKey))
            {
                module.Uninstall();
                return;
            }

            try
            {
                if (!module.Install())
                {
                    Log.Warning(
                        "[FMPO] Compatibility module " + definition.moduleKey
                        + " could not recognize the installed target version and "
                        + "was left inactive.");
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    "[FMPO] Compatibility module " + definition.moduleKey
                    + " failed safely and was left inactive. " + exception);
            }
        }

        private static void CloseOpenFloatMenu()
        {
            WindowStack windowStack = Find.WindowStack;
            FloatMenu floatMenu = windowStack?.FloatMenu;
            if (floatMenu != null)
            {
                windowStack.TryRemove(floatMenu, true);
            }
        }

        private static bool IsAssemblyLoaded(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

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

        internal static bool HasPatchOwner(
            MethodBase target,
            IEnumerable<string> owners)
        {
            if (target == null)
            {
                return false;
            }

            Patches patchInfo = Harmony.GetPatchInfo(target);
            return patchInfo != null
                && owners.Any(owner => patchInfo.Owners.Contains(owner));
        }

        internal static void UnpatchOwners(
            IEnumerable<MethodBase> targets,
            IEnumerable<string> owners)
        {
            Harmony controller = new Harmony(ControllerHarmonyId);
            foreach (MethodBase target in targets.Where(target => target != null).Distinct())
            {
                Patches patchInfo = Harmony.GetPatchInfo(target);
                if (patchInfo == null)
                {
                    continue;
                }

                foreach (string owner in owners)
                {
                    if (!patchInfo.Owners.Contains(owner))
                    {
                        continue;
                    }

                    controller.Unpatch(target, HarmonyPatchType.All, owner);
                    Log.Message(
                        "[FMPO] Removed compatibility patches owned by "
                        + owner + " on " + target.DeclaringType?.FullName + "."
                        + target.Name);
                }
            }
        }

        internal static ThingComp FindComp(Thing thing, Type compType)
        {
            ThingWithComps thingWithComps = thing as ThingWithComps;
            List<ThingComp> comps = thingWithComps?.AllComps;
            if (compType == null || comps == null)
            {
                return null;
            }

            for (int index = 0; index < comps.Count; index++)
            {
                if (compType.IsInstanceOfType(comps[index]))
                {
                    return comps[index];
                }
            }

            return null;
        }

        internal static MemberInfo FindInstanceMember(Type type, string name)
        {
            if (type == null)
            {
                return null;
            }

            return (MemberInfo)AccessTools.Field(type, name)
                ?? AccessTools.Property(type, name);
        }

        internal static object ReadMember(MemberInfo member, object instance)
        {
            FieldInfo field = member as FieldInfo;
            if (field != null)
            {
                return field.GetValue(instance);
            }

            PropertyInfo property = member as PropertyInfo;
            return property?.GetValue(instance, null);
        }
    }
}
