using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FloatMenuRevalidationControl.Compatibility.RigorMortis
{
    internal static class RigorMortisCompatibilityModule
    {
        private const string HarmonyId =
            "vorsel.floatmenuperformanceoptimizer.compatibility.rigormortis";
        private const string RecoverWorkGiverDefName = "RM_Recover";
        private const string AxolotlReadWorkGiverDefName =
            "Axolotl_ReadMoeLotlQiSkillBooks";
        private const string AxolotlSkillBookTypeName =
            "Axolotl.MoeLotlSkillBook";
        private const string ZombieCasketTypeName =
            "RigorMortis.Building_ZombieCasket";
        private const string IncantationCompPropertiesTypeName =
            "RigorMortis.CompProperties_Incantation";
        private const string PutWorkerTypeName =
            "RigorMortis.PawnRenderNodeWorker_IncantationPut";
        private const string ExecuteWorkerTypeName =
            "RigorMortis.PawnRenderNodeWorker_IncantationExecute";
        private const string PutHediffTypeName =
            "RigorMortis.HediffAbility_IncantationPut";
        private const string ExecuteHediffTypeName =
            "RigorMortis.HediffAbility_IncantationExecute";

        private static readonly string[] LegacyHarmonyOwners =
        {
            "kaga.rigormortis.floatmenufix",
            "vorsel.rigormortis.floatmenufix"
        };

        private static readonly Dictionary<string, Graphic> Graphics =
            new Dictionary<string, Graphic>();

        private static Type zombieCasketType;
        private static Type putHediffType;
        private static Type executeHediffType;
        private static MemberInfo putGraphicPathMember;
        private static MemberInfo executeGraphicPathMember;
        private static bool installed;

        internal static bool Install()
        {
            if (installed)
            {
                return true;
            }

            MethodBase workGiverOptionMethod = AccessTools.Method(
                typeof(FloatMenuOptionProvider_WorkGivers),
                "GetWorkGiverOption");
            if (workGiverOptionMethod == null)
            {
                return false;
            }

            if (CompatibilityManager.HasPatchOwner(
                workGiverOptionMethod,
                LegacyHarmonyOwners))
            {
                return true;
            }

            if (CompatibilityManager.HasPatchOwner(
                workGiverOptionMethod,
                new[] { HarmonyId }))
            {
                installed = true;
                return true;
            }

            zombieCasketType = AccessTools.TypeByName(ZombieCasketTypeName);
            Harmony harmony = new Harmony(HarmonyId);
            harmony.Patch(
                workGiverOptionMethod,
                prefix: new HarmonyMethod(
                    typeof(RigorMortisCompatibilityModule),
                    nameof(WorkGiverOptionPrefix)));

            bool graphicsEnabled = TryInstallGraphicCompatibility(harmony);
            installed = true;
            Log.Message(
                "[FMPO] Rigor Mortis compatibility optimization enabled"
                + (graphicsEnabled
                    ? ", including incantation graphic preloading."
                    : "; incantation graphic API was not recognized and was skipped."));
            return true;
        }

        internal static void Uninstall()
        {
            CompatibilityManager.UnpatchOwners(
                LegacyTargets(),
                new[] { HarmonyId });
            installed = false;
            zombieCasketType = null;
            putHediffType = null;
            executeHediffType = null;
            putGraphicPathMember = null;
            executeGraphicPathMember = null;
        }

        internal static void DisableLegacyPatches()
        {
            CompatibilityManager.UnpatchOwners(
                LegacyTargets(),
                LegacyHarmonyOwners);
        }

        private static IEnumerable<MethodBase> LegacyTargets()
        {
            yield return AccessTools.Method(
                typeof(FloatMenuOptionProvider_WorkGivers),
                "GetWorkGiverOption");
            yield return AccessTools.Method(
                AccessTools.TypeByName(PutWorkerTypeName),
                "GetGraphic");
            yield return AccessTools.Method(
                AccessTools.TypeByName(ExecuteWorkerTypeName),
                "GetGraphic");
        }

        private static bool WorkGiverOptionPrefix(
            WorkGiverDef workGiver,
            LocalTargetInfo target,
            ref FloatMenuOption __result)
        {
            string defName = workGiver?.defName;
            Thing thing = target.Thing;

            if (defName == RecoverWorkGiverDefName
                && zombieCasketType != null
                && !zombieCasketType.IsInstanceOfType(thing))
            {
                __result = null;
                return false;
            }

            if (defName == AxolotlReadWorkGiverDefName
                && !IsTypeOrSubclass(thing, AxolotlSkillBookTypeName))
            {
                __result = null;
                return false;
            }

            return true;
        }

        private static bool TryInstallGraphicCompatibility(Harmony harmony)
        {
            Type compPropertiesType = AccessTools.TypeByName(
                IncantationCompPropertiesTypeName);
            Type putWorkerType = AccessTools.TypeByName(PutWorkerTypeName);
            Type executeWorkerType = AccessTools.TypeByName(ExecuteWorkerTypeName);
            putHediffType = AccessTools.TypeByName(PutHediffTypeName);
            executeHediffType = AccessTools.TypeByName(ExecuteHediffTypeName);

            MemberInfo texPathMember = CompatibilityManager.FindInstanceMember(
                compPropertiesType,
                "texPath");
            putGraphicPathMember = CompatibilityManager.FindInstanceMember(
                putHediffType,
                "graphicPath");
            executeGraphicPathMember = CompatibilityManager.FindInstanceMember(
                executeHediffType,
                "graphicPath");
            MethodBase putGraphicMethod = AccessTools.Method(
                putWorkerType,
                "GetGraphic");
            MethodBase executeGraphicMethod = AccessTools.Method(
                executeWorkerType,
                "GetGraphic");

            if (compPropertiesType == null
                || texPathMember == null
                || putHediffType == null
                || executeHediffType == null
                || putGraphicPathMember == null
                || executeGraphicPathMember == null
                || putGraphicMethod == null
                || executeGraphicMethod == null)
            {
                return false;
            }

            PreloadIncantationGraphics(compPropertiesType, texPathMember);
            HarmonyMethod prefix = new HarmonyMethod(
                typeof(RigorMortisCompatibilityModule),
                nameof(RenderGraphicPrefix));
            harmony.Patch(putGraphicMethod, prefix: prefix);
            harmony.Patch(executeGraphicMethod, prefix: prefix);
            return true;
        }

        private static void PreloadIncantationGraphics(
            Type compPropertiesType,
            MemberInfo texPathMember)
        {
            List<ThingDef> definitions = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int definitionIndex = 0;
                definitionIndex < definitions.Count;
                definitionIndex++)
            {
                List<CompProperties> comps = definitions[definitionIndex].comps;
                if (comps == null)
                {
                    continue;
                }

                for (int compIndex = 0; compIndex < comps.Count; compIndex++)
                {
                    CompProperties comp = comps[compIndex];
                    if (!compPropertiesType.IsInstanceOfType(comp))
                    {
                        continue;
                    }

                    string path = CompatibilityManager.ReadMember(
                        texPathMember,
                        comp) as string;
                    if (string.IsNullOrEmpty(path) || Graphics.ContainsKey(path))
                    {
                        continue;
                    }

                    Graphics.Add(
                        path,
                        GraphicDatabase.Get<Graphic_Multi>(
                            path,
                            ShaderDatabase.Cutout,
                            Vector2.one,
                            Color.white));
                }
            }
        }

        private static bool RenderGraphicPrefix(
            PawnRenderNode node,
            ref Graphic __result)
        {
            object hediff = node?.hediff;
            string path = null;
            if (putHediffType.IsInstanceOfType(hediff))
            {
                path = CompatibilityManager.ReadMember(
                    putGraphicPathMember,
                    hediff) as string;
            }
            else if (executeHediffType.IsInstanceOfType(hediff))
            {
                path = CompatibilityManager.ReadMember(
                    executeGraphicPathMember,
                    hediff) as string;
            }

            Graphic graphic;
            if (string.IsNullOrEmpty(path)
                || !Graphics.TryGetValue(path, out graphic))
            {
                return true;
            }

            __result = graphic;
            return false;
        }

        private static bool IsTypeOrSubclass(Thing thing, string expectedFullName)
        {
            Type type = thing?.GetType();
            while (type != null)
            {
                if (type.FullName == expectedFullName)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }
    }
}
