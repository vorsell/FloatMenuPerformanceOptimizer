using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FloatMenuRevalidationControl.Compatibility.WingsOfDemocracy
{
    internal static class WingsOfDemocracyCompatibilityModule
    {
        private const string HarmonyId =
            "vorsel.floatmenuperformanceoptimizer.compatibility.wingsofdemocracy";
        private const string SolarWorkGiverDefName =
            "PLAMilira_EmptySolarCrystalContainer";
        private const string DuplicateWorkGiverDefName = "PLAMilira_PoisonFood";
        private const string RearmWorkGiverDefName = "RearmPLAMiliraBuildings";
        private const string SolarTowerDefName =
            "Milira_SolarCrystalGatheringTower";
        private const string GeneratorTypeName =
            "PLAMilira.CompGenerator_SolarCrystal";

        private static readonly string[] LegacyHarmonyOwners =
        {
            "vorsel.milirawingsofdemocracy.floatmenufix"
        };

        private static readonly long CacheDurationTicks = Stopwatch.Frequency / 4;

        private static Type generatorType;
        private static MemberInfo canEmptyNowMember;
        private static MethodBase workGiverOptionMethod;
        private static Pawn cachedPawn;
        private static Thing cachedTower;
        private static FloatMenuOption cachedOption;
        private static long cacheValidUntil;
        private static bool installed;

        internal static bool Install()
        {
            if (installed)
            {
                return true;
            }

            workGiverOptionMethod = AccessTools.Method(
                typeof(FloatMenuOptionProvider_WorkGivers),
                "GetWorkGiverOption");
            generatorType = AccessTools.TypeByName(GeneratorTypeName);
            canEmptyNowMember = CompatibilityManager.FindInstanceMember(
                generatorType,
                "CanEmptyNow");

            if (workGiverOptionMethod == null
                || generatorType == null
                || canEmptyNowMember == null)
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

            new Harmony(HarmonyId).Patch(
                workGiverOptionMethod,
                prefix: new HarmonyMethod(
                    typeof(WingsOfDemocracyCompatibilityModule),
                    nameof(WorkGiverOptionPrefix)),
                postfix: new HarmonyMethod(
                    typeof(WingsOfDemocracyCompatibilityModule),
                    nameof(WorkGiverOptionPostfix)));

            installed = true;
            Log.Message(
                "[FMPO] Milira: Wings of Democracy compatibility optimization enabled.");
            return true;
        }

        internal static void Uninstall()
        {
            CompatibilityManager.UnpatchOwners(
                LegacyTargets(),
                new[] { HarmonyId });
            installed = false;
            generatorType = null;
            canEmptyNowMember = null;
            workGiverOptionMethod = null;
            cachedPawn = null;
            cachedTower = null;
            cachedOption = null;
            cacheValidUntil = 0;
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
        }

        private static bool WorkGiverOptionPrefix(
            Pawn pawn,
            WorkGiverDef workGiver,
            LocalTargetInfo target,
            ref FloatMenuOption __result,
            out bool __state)
        {
            __state = false;
            if (workGiver == null)
            {
                return true;
            }

            Thing clickedThing = target.Thing;
            if (workGiver.defName == RearmWorkGiverDefName
                && pawn != null
                && !pawn.Drafted
                && clickedThing?.def?.defName == SolarTowerDefName)
            {
                __result = null;
                return false;
            }

            if (workGiver.defName == DuplicateWorkGiverDefName)
            {
                __result = null;
                return false;
            }

            if (workGiver.defName != SolarWorkGiverDefName)
            {
                return true;
            }

            Thing tower = clickedThing;
            ThingComp generator = CompatibilityManager.FindComp(tower, generatorType);
            if (tower?.def?.defName != SolarTowerDefName || generator == null)
            {
                __result = null;
                return false;
            }

            if (!tower.Spawned || !ReadCanEmptyNow(generator))
            {
                Invalidate(pawn, tower);
                __result = null;
                return false;
            }

            long now = Stopwatch.GetTimestamp();
            if (ReferenceEquals(cachedPawn, pawn)
                && ReferenceEquals(cachedTower, tower)
                && now <= cacheValidUntil)
            {
                __result = cachedOption;
                return false;
            }

            __state = true;
            return true;
        }

        private static void WorkGiverOptionPostfix(
            Pawn pawn,
            LocalTargetInfo target,
            ref FloatMenuOption __result,
            bool __state)
        {
            if (!__state)
            {
                return;
            }

            cachedPawn = pawn;
            cachedTower = target.Thing;
            cachedOption = __result;
            cacheValidUntil = Stopwatch.GetTimestamp() + CacheDurationTicks;
        }

        private static bool ReadCanEmptyNow(ThingComp generator)
        {
            object value = CompatibilityManager.ReadMember(
                canEmptyNowMember,
                generator);
            return value is bool && (bool)value;
        }

        private static void Invalidate(Pawn pawn, Thing tower)
        {
            if (ReferenceEquals(cachedPawn, pawn)
                && ReferenceEquals(cachedTower, tower))
            {
                cachedOption = null;
                cacheValidUntil = 0;
            }
        }
    }
}
