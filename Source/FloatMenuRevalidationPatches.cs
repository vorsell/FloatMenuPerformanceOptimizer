using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FloatMenuRevalidationControl
{
    [StaticConstructorOnStartup]
    internal static class FloatMenuRevalidationControlStartup
    {
        static FloatMenuRevalidationControlStartup()
        {
            Harmony harmony = new Harmony(
                "vorsel.floatmenuperformanceoptimizer");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[FMPO] Float Menu Performance Optimizer initialized.");
        }
    }

    internal static class RevalidationRuntime
    {
        internal const int VanillaIntervalFrames = 4;
        internal const int MinimumIntervalFrames = 1;
        internal const int DefaultTimedIntervalFrames = 30;
        internal const int MinimumAdaptiveIntervalFrames = 4;
        internal const int DefaultAdaptiveIntervalHundredths = 50;

        private sealed class MenuScheduleState
        {
            internal bool Initialized;
            internal RevalidationMode Mode;
            internal int SettingValue;
            internal int ResetGeneration;
            internal int LastFrame;
            internal float LastRealtime;
            internal int BatchFrameEstimate;
        }

        private static readonly FloatMenuRevalidationSettings DefaultSettings =
            new FloatMenuRevalidationSettings();

        private static readonly ConditionalWeakTable<FloatMenuMap, MenuScheduleState>
            MenuSchedules =
                new ConditionalWeakTable<FloatMenuMap, MenuScheduleState>();

        private static RevalidationMode lastReportedMode = (RevalidationMode)(-1);
        private static int lastReportedValue = int.MinValue;
        private static int scheduleResetGeneration;

        [ThreadStatic]
        private static int activeBatchFrameEstimate;

        internal static FloatMenuRevalidationSettings Settings
        {
            get
            {
                return FloatMenuRevalidationControlMod.CurrentSettings ?? DefaultSettings;
            }
        }

        internal static bool SkipOpenMenuRevalidation()
        {
            return Settings.Mode == RevalidationMode.Lazy;
        }

        [ThreadStatic]
        private static FloatMenu pendingKeepOpenMenu;

        [ThreadStatic]
        private static FloatMenuOption pendingKeepOpenOption;

        internal static void ReportActiveModeIfChanged()
        {
            RevalidationMode mode = Settings.Mode;
            int value = ModeSettingValue(mode);
            if (mode == lastReportedMode && value == lastReportedValue)
            {
                return;
            }

            lastReportedMode = mode;
            lastReportedValue = value;
            if (mode == RevalidationMode.Lazy)
            {
                Log.Message("[FMPO] Active mode: Lazy. Open-menu revalidation is bypassed; click validation remains active.");
            }
            else if (mode == RevalidationMode.Periodic)
            {
                Log.Message("[FMPO] Active mode: Timed, full regeneration every " + value + " rendered frames.");
            }
            else if (mode == RevalidationMode.Adaptive)
            {
                Log.Message(
                    "[FMPO] Active mode: Adaptive, target interval "
                    + EffectiveAdaptiveIntervalSeconds().ToString("0.00")
                    + " real seconds with a 4-frame minimum.");
            }
            else
            {
                Log.Message("[FMPO] Active mode: Disabled (vanilla behavior).");
            }
        }

        internal static void RequestKeepOpenAfterRejectedClick(
            FloatMenu menu,
            FloatMenuOption option)
        {
            pendingKeepOpenMenu = menu;
            pendingKeepOpenOption = option;
        }

        internal static bool ConsumeKeepOpenRequest(
            FloatMenu menu,
            FloatMenuOption option)
        {
            bool matches = ReferenceEquals(pendingKeepOpenMenu, menu)
                && ReferenceEquals(pendingKeepOpenOption, option);
            if (matches)
            {
                pendingKeepOpenMenu = null;
                pendingKeepOpenOption = null;
            }

            return matches;
        }

        internal static int RegenerationGate(FloatMenuMap menu)
        {
            RevalidationMode mode = Settings.Mode;
            if (mode == RevalidationMode.Disabled)
            {
                activeBatchFrameEstimate = VanillaIntervalFrames;
                return Time.frameCount % VanillaIntervalFrames;
            }

            if (mode != RevalidationMode.Periodic
                && mode != RevalidationMode.Adaptive)
            {
                activeBatchFrameEstimate = VanillaIntervalFrames;
                return 1;
            }

            int nowFrame = Time.frameCount;
            float nowRealtime = Time.realtimeSinceStartup;
            int settingValue = ModeSettingValue(mode);
            MenuScheduleState state = MenuSchedules.GetValue(
                menu,
                CreateMenuScheduleState);

            if (!state.Initialized
                || state.Mode != mode
                || state.SettingValue != settingValue
                || state.ResetGeneration != scheduleResetGeneration
                || nowRealtime < state.LastRealtime)
            {
                ResetScheduleState(
                    state,
                    mode,
                    settingValue,
                    nowFrame,
                    nowRealtime);
                activeBatchFrameEstimate = state.BatchFrameEstimate;
                return 1;
            }

            uint elapsedFrames = unchecked((uint)(nowFrame - state.LastFrame));
            bool due;
            if (mode == RevalidationMode.Periodic)
            {
                due = elapsedFrames >= (uint)FullRegenerationIntervalFrames();
            }
            else
            {
                due = elapsedFrames >= MinimumAdaptiveIntervalFrames
                    && nowRealtime - state.LastRealtime
                        >= EffectiveAdaptiveIntervalSeconds();
            }

            if (due)
            {
                state.LastFrame = nowFrame;
                state.LastRealtime = nowRealtime;
                state.BatchFrameEstimate = mode == RevalidationMode.Periodic
                    ? FullRegenerationIntervalFrames()
                    : Math.Max(
                        MinimumAdaptiveIntervalFrames,
                        elapsedFrames > int.MaxValue
                            ? int.MaxValue
                            : (int)elapsedFrames);
            }

            activeBatchFrameEstimate = state.BatchFrameEstimate;
            return due ? 0 : 1;
        }

        internal static int FullRegenerationIntervalFrames()
        {
            if (Settings.Mode != RevalidationMode.Periodic)
            {
                return VanillaIntervalFrames;
            }

            return Math.Max(
                Settings.PeriodicIntervalFrames,
                MinimumIntervalFrames);
        }

        internal static float EffectiveAdaptiveIntervalSeconds()
        {
            int hundredths = Settings.AdaptiveIntervalHundredths;
            if (hundredths <= 0)
            {
                hundredths = DefaultAdaptiveIntervalHundredths;
            }

            return hundredths / 100f;
        }

        internal static bool ShouldBatchOptions(bool hasAtLeastThreeOptions)
        {
            if (!hasAtLeastThreeOptions)
            {
                return false;
            }

            return Settings.Mode == RevalidationMode.Adaptive
                || FullRegenerationIntervalFrames() > 1;
        }

        internal static float BatchDivisor()
        {
            return Math.Max(1, activeBatchFrameEstimate - 1);
        }

        internal static void NotifySaveFinished()
        {
            unchecked
            {
                scheduleResetGeneration++;
            }
        }

        private static int ModeSettingValue(RevalidationMode mode)
        {
            if (mode == RevalidationMode.Periodic)
            {
                return FullRegenerationIntervalFrames();
            }

            if (mode == RevalidationMode.Adaptive)
            {
                return Math.Max(0, Settings.AdaptiveIntervalHundredths);
            }

            return 0;
        }

        private static MenuScheduleState CreateMenuScheduleState(
            FloatMenuMap menu)
        {
            return new MenuScheduleState();
        }

        private static void ResetScheduleState(
            MenuScheduleState state,
            RevalidationMode mode,
            int settingValue,
            int nowFrame,
            float nowRealtime)
        {
            state.Initialized = true;
            state.Mode = mode;
            state.SettingValue = settingValue;
            state.ResetGeneration = scheduleResetGeneration;
            state.LastFrame = nowFrame;
            state.LastRealtime = nowRealtime;
            state.BatchFrameEstimate = mode == RevalidationMode.Periodic
                ? FullRegenerationIntervalFrames()
                : InitialAdaptiveFrameEstimate(
                    EffectiveAdaptiveIntervalSeconds());
        }

        private static int InitialAdaptiveFrameEstimate(float seconds)
        {
            double estimate = Math.Ceiling(seconds * 60d);
            if (estimate >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return Math.Max(
                MinimumAdaptiveIntervalFrames,
                (int)estimate);
        }
    }

    [HarmonyPatch(typeof(FloatMenuMap), "DoWindowContents")]
    internal static class FloatMenuMapWindowPatch
    {
        private delegate void BaseWindowDraw(FloatMenu instance, Rect inRect);

        private static readonly BaseWindowDraw DrawBaseWindow = CreateBaseWindowDraw();

        private static bool Prefix(
            FloatMenuMap __instance,
            [HarmonyArgument(0)] Rect inRect)
        {
            RevalidationRuntime.ReportActiveModeIfChanged();
            if (!RevalidationRuntime.SkipOpenMenuRevalidation())
            {
                return true;
            }

            if (!Find.Selector.AnyPawnSelected)
            {
                Find.WindowStack.TryRemove(__instance, doCloseSound: true);
                return false;
            }

            // FloatMenuMap only adds live option revalidation around the base
            // FloatMenu drawing code. Calling the base implementation directly
            // gives Lazy mode a true opening snapshot while preserving normal
            // drawing, input, and FloatMenuMap.PreOptionChosen click validation.
            DrawBaseWindow(__instance, inRect);
            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            List<CodeInstruction> codes = instructions.ToList();
            MethodInfo frameCountGetter = AccessTools.PropertyGetter(typeof(Time), "frameCount");
            MethodInfo regenerationGateMethod = AccessTools.Method(
                typeof(RevalidationRuntime),
                "RegenerationGate");
            MethodInfo batchModeMethod = AccessTools.Method(
                typeof(RevalidationRuntime),
                "ShouldBatchOptions");
            MethodInfo batchDivisorMethod = AccessTools.Method(
                typeof(RevalidationRuntime),
                "BatchDivisor");

            int frameGetterIndex = FindCall(codes, frameCountGetter);
            int batchComparisonEndIndex = FindBatchComparisonEnd(codes, frameGetterIndex);
            int batchDivisorIndex = FindFloatConstant(codes, 3f, frameGetterIndex + 1);

            bool intervalPatternValid = frameGetterIndex >= 0
                && frameGetterIndex + 2 < codes.Count
                && IsLoadInt(
                    codes[frameGetterIndex + 1],
                    RevalidationRuntime.VanillaIntervalFrames)
                && codes[frameGetterIndex + 2].opcode == OpCodes.Rem;

            if (!intervalPatternValid
                || batchComparisonEndIndex < 0
                || batchDivisorIndex < 0)
            {
                Log.Error("[FMPO] FloatMenuMap.DoWindowContents layout was not recognized; leaving vanilla behavior unchanged.");
                return codes;
            }

            codes[frameGetterIndex] = ReplaceWithInstruction(
                codes[frameGetterIndex],
                OpCodes.Ldarg_0);
            codes[frameGetterIndex + 1] = ReplaceWithCall(
                codes[frameGetterIndex + 1],
                regenerationGateMethod);
            codes[frameGetterIndex + 2] = ReplaceWithInstruction(
                codes[frameGetterIndex + 2],
                OpCodes.Nop);
            codes[batchDivisorIndex] = ReplaceWithCall(
                codes[batchDivisorIndex],
                batchDivisorMethod);

            codes.Insert(
                batchComparisonEndIndex + 1,
                new CodeInstruction(OpCodes.Call, batchModeMethod));

            return codes;
        }

        private static BaseWindowDraw CreateBaseWindowDraw()
        {
            MethodInfo method = AccessTools.Method(
                typeof(FloatMenu),
                "DoWindowContents",
                new[] { typeof(Rect) });
            DynamicMethod dynamicMethod = new DynamicMethod(
                "FMRC_FloatMenu_DoWindowContents_BaseCall",
                typeof(void),
                new[] { typeof(FloatMenu), typeof(Rect) },
                typeof(FloatMenuMapWindowPatch),
                skipVisibility: true);
            ILGenerator il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
            return (BaseWindowDraw)dynamicMethod.CreateDelegate(typeof(BaseWindowDraw));
        }

        private static int FindCall(List<CodeInstruction> codes, MethodInfo method)
        {
            for (int index = 0; index < codes.Count; index++)
            {
                if (codes[index].Calls(method))
                {
                    return index;
                }
            }

            return -1;
        }

        private static int FindBatchComparisonEnd(List<CodeInstruction> codes, int beforeIndex)
        {
            for (int index = 4; index < beforeIndex; index++)
            {
                if (codes[index - 4].opcode == OpCodes.Ldc_I4_3
                    && codes[index - 3].opcode == OpCodes.Clt
                    && codes[index - 2].opcode == OpCodes.Ldc_I4_0
                    && codes[index - 1].opcode == OpCodes.Ceq)
                {
                    return index - 1;
                }
            }

            return -1;
        }

        private static int FindFloatConstant(
            List<CodeInstruction> codes,
            float value,
            int startIndex)
        {
            for (int index = Math.Max(0, startIndex); index < codes.Count; index++)
            {
                if (codes[index].opcode == OpCodes.Ldc_R4
                    && codes[index].operand is float
                    && Math.Abs((float)codes[index].operand - value) < 0.001f)
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool IsLoadInt(CodeInstruction code, int value)
        {
            if (value == 4 && code.opcode == OpCodes.Ldc_I4_4)
            {
                return true;
            }

            if (code.opcode == OpCodes.Ldc_I4 && code.operand is int)
            {
                return (int)code.operand == value;
            }

            if (code.opcode == OpCodes.Ldc_I4_S && code.operand is sbyte)
            {
                return (sbyte)code.operand == value;
            }

            return false;
        }

        private static CodeInstruction ReplaceWithCall(
            CodeInstruction original,
            MethodInfo method)
        {
            CodeInstruction replacement = new CodeInstruction(OpCodes.Call, method);
            replacement.labels.AddRange(original.labels);
            replacement.blocks.AddRange(original.blocks);
            return replacement;
        }

        private static CodeInstruction ReplaceWithInstruction(
            CodeInstruction original,
            OpCode opcode)
        {
            CodeInstruction replacement = new CodeInstruction(opcode);
            replacement.labels.AddRange(original.labels);
            replacement.blocks.AddRange(original.blocks);
            return replacement;
        }
    }

    [HarmonyPatch(typeof(GameDataSaveLoader), "SaveGame", new[] { typeof(string) })]
    internal static class SaveFinishedScheduleResetPatch
    {
        private static void Postfix()
        {
            RevalidationRuntime.NotifySaveFinished();
        }
    }

    [HarmonyPatch(typeof(FloatMenuOption), "DoGUI")]
    internal static class RejectedClickMenuLifetimePatch
    {
        private static void Postfix(
            FloatMenuOption __instance,
            [HarmonyArgument(2)] FloatMenu floatMenu,
            ref bool __result)
        {
            if (RevalidationRuntime.ConsumeKeepOpenRequest(floatMenu, __instance))
            {
                // DoGUI normally returns true for every accepted button press,
                // which makes FloatMenu close even when PreOptionChosen has just
                // rejected and disabled the option. False keeps this menu open.
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(FloatMenuMap), "PreOptionChosen")]
    internal static class ClickValidationPatch
    {
        private static readonly FieldInfo CachedChoicesField =
            AccessTools.Field(typeof(FloatMenuMap), "cachedChoices");

        private static void Prefix(
            [HarmonyArgument(0)] FloatMenuOption option,
            out ClickValidationState __state)
        {
            bool clickValidationEnabled =
                RevalidationRuntime.Settings.Mode != RevalidationMode.Disabled;
            if (clickValidationEnabled)
            {
                Dictionary<Vector3, List<FloatMenuOption>> cachedChoices =
                    CachedChoicesField.GetValue(null)
                    as Dictionary<Vector3, List<FloatMenuOption>>;
                if (cachedChoices != null)
                {
                    cachedChoices.Clear();
                }
            }

            bool wasEnabled = clickValidationEnabled
                && option != null
                && !option.Disabled;
            __state = new ClickValidationState
            {
                WasEnabled = wasEnabled,
                ShowMessage = wasEnabled
                    && RevalidationRuntime.Settings.ShowClickValidationFailureMessage
            };
        }

        private static void Postfix(
            FloatMenuMap __instance,
            [HarmonyArgument(0)] FloatMenuOption option,
            ClickValidationState __state)
        {
            if (!__state.WasEnabled || option == null || !option.Disabled)
            {
                return;
            }

            RevalidationRuntime.RequestKeepOpenAfterRejectedClick(
                __instance,
                option);
            if (!__state.ShowMessage)
            {
                return;
            }

            Messages.Message(
                FailureMessage(option),
                MessageTypeDefOf.RejectInput,
                historical: false);
        }

        private static string FailureMessage(FloatMenuOption option)
        {
            Thing target = option.revalidateClickTarget;
            if (target != null && !option.targetsDespawned && !target.Spawned)
            {
                return "FMRC_TargetGone".Translate().ToString();
            }

            return "FMRC_ActionUnavailable".Translate().ToString();
        }

        private struct ClickValidationState
        {
            internal bool WasEnabled;
            internal bool ShowMessage;
        }
    }
}
