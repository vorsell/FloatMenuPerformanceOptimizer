using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FloatMenuRevalidationControl.Compatibility.MiliraRace
{
    internal static class MiliraRaceCompatibilityModule
    {
        private const string HarmonyId =
            "vorsel.floatmenuperformanceoptimizer.compatibility.milirarace";
        private const string WorkGiverDefName =
            "Milira_EmptySunLightFuelContainer";
        private const string GeneratorTypeName =
            "Milira.CompGenerator_SunLightFuel";
        private const string WorkerTypeName =
            "Milira.WorkGiver_EmptySunLightFuelContainer";

        private static readonly string[] LegacyHarmonyOwners =
        {
            "vorsel.milirarace.floatmenufix"
        };

        private static readonly long CacheDurationTicks = Stopwatch.Frequency / 4;

        [ThreadStatic]
        private static bool evaluatingTower;

        private static Type generatorType;
        private static MemberInfo canEmptyNowMember;
        private static MethodBase workGiverOptionMethod;
        private static MethodBase shouldSkipMethod;
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
            Type workerType = AccessTools.TypeByName(WorkerTypeName);
            shouldSkipMethod = AccessTools.Method(workerType, "ShouldSkip");
            canEmptyNowMember = CompatibilityManager.FindInstanceMember(
                generatorType,
                "CanEmptyNow");

            if (workGiverOptionMethod == null
                || generatorType == null
                || shouldSkipMethod == null
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

            Harmony harmony = new Harmony(HarmonyId);
            harmony.Patch(
                workGiverOptionMethod,
                prefix: new HarmonyMethod(
                    typeof(MiliraRaceCompatibilityModule),
                    nameof(WorkGiverOptionPrefix)),
                postfix: new HarmonyMethod(
                    typeof(MiliraRaceCompatibilityModule),
                    nameof(WorkGiverOptionPostfix)),
                finalizer: new HarmonyMethod(
                    typeof(MiliraRaceCompatibilityModule),
                    nameof(WorkGiverOptionFinalizer)));
            harmony.Patch(
                shouldSkipMethod,
                prefix: new HarmonyMethod(
                    typeof(MiliraRaceCompatibilityModule),
                    nameof(ShouldSkipPrefix)));

            installed = true;
            Log.Message("[FMPO] Milira Race compatibility optimization enabled.");
            return true;
        }

        internal static void Uninstall()
        {
            CompatibilityManager.UnpatchOwners(
                LegacyTargets(),
                new[] { HarmonyId });
            installed = false;
            evaluatingTower = false;
            generatorType = null;
            canEmptyNowMember = null;
            workGiverOptionMethod = null;
            shouldSkipMethod = null;
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
            yield return AccessTools.Method(
                AccessTools.TypeByName(WorkerTypeName),
                "ShouldSkip");
        }

        private static bool WorkGiverOptionPrefix(
            Pawn pawn,
            WorkGiverDef workGiver,
            LocalTargetInfo target,
            ref FloatMenuOption __result,
            out bool __state)
        {
            __state = false;
            if (workGiver == null || workGiver.defName != WorkGiverDefName)
            {
                return true;
            }

            Thing tower = target.Thing;
            ThingComp generator = CompatibilityManager.FindComp(tower, generatorType);
            if (generator == null)
            {
                __result = null;
                return false;
            }

            if (!tower.Spawned || !ReadCanEmptyNow(generator))
            {
                Invalidate(tower, pawn);
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

            evaluatingTower = true;
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

            evaluatingTower = false;
            cachedPawn = pawn;
            cachedTower = target.Thing;
            cachedOption = __result;
            cacheValidUntil = Stopwatch.GetTimestamp() + CacheDurationTicks;
        }

        private static Exception WorkGiverOptionFinalizer(
            Exception __exception,
            bool __state)
        {
            if (__state)
            {
                evaluatingTower = false;
            }

            return __exception;
        }

        private static bool ShouldSkipPrefix(ref bool __result)
        {
            if (!evaluatingTower)
            {
                return true;
            }

            __result = false;
            return false;
        }

        private static bool ReadCanEmptyNow(ThingComp generator)
        {
            object value = CompatibilityManager.ReadMember(
                canEmptyNowMember,
                generator);
            return value is bool && (bool)value;
        }

        private static void Invalidate(Thing tower, Pawn pawn)
        {
            if (ReferenceEquals(cachedTower, tower)
                && ReferenceEquals(cachedPawn, pawn))
            {
                cacheValidUntil = 0;
                cachedOption = null;
            }
        }
    }
}
