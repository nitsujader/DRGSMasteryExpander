using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponMasteryExpander
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class WeaponMasteryExpander : BasePlugin
    {
        private const string PLUGIN_GUID = "com.drgs.weaponmasteryexpander";
        private const string PLUGIN_NAME = "Weapon Mastery Expander";
        private const string PLUGIN_VERSION = "1.0.0";

        private static ConfigEntry<int> _targetStages;
        private static ConfigEntry<bool> _enableMod;
        private new static ManualLogSource Log;

        // Track whether we're in a weapon mastery run
        private static bool _inWeaponMasteryRun = false;

        public override void Load()
        {
            Log = base.Log;
            _enableMod = Config.Bind("General", "Enable Mod", true, "Enable/disable");
            _targetStages = Config.Bind("General", "Target Stages", 10,
                new ConfigDescription("Number of stages for weapon mastery (vanilla is 3)",
                    new AcceptableValueRange<int>(3, 20)));
            Harmony.CreateAndPatchAll(typeof(WeaponMasteryExpander));
            Log.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded! Target stages: {_targetStages.Value}");
        }

        // ============================================================
        // Hook 1: Detect when weapon mastery challenge set is selected
        // ============================================================
        [HarmonyPatch(typeof(ChallengeManager), "SetChallengeDataSet")]
        [HarmonyPostfix]
        private static void SetChallengeDataSetPostfix(ChallengeDataSet challengeDataSet)
        {
            if (!_enableMod.Value) return;
            try
            {
                _inWeaponMasteryRun = challengeDataSet != null && challengeDataSet.EChallengeType == EChallengeType.WeaponMastery;
                Log.LogInfo($"[SetChallengeDataSet] type={challengeDataSet?.EChallengeType}, weaponMastery={_inWeaponMasteryRun}");
            }
            catch (Exception ex)
            {
                Log.LogError($"[SetChallengeDataSet] Error: {ex.Message}");
            }
        }

        // ============================================================
        // Hook 2: After weapon mastery challenge applies its config to the run
        // ============================================================
        [HarmonyPatch(typeof(ChallengeDataWeaponSlots), "Apply")]
        [HarmonyPostfix]
        private static void WeaponSlotsApplyPostfix(ChallengeDataWeaponSlots __instance, RunMods mods, RunSettingsManager runSettingsManager)
        {
            if (!_enableMod.Value) return;
            try
            {
                int targetStages = _targetStages.Value;
                string weaponName = "???";
                try { weaponName = __instance.GetTitle() ?? "null"; } catch { }

                Log.LogInfo($"[Apply] Weapon: {weaponName}");

                // Expand the MissionMapConfig that Apply() stored in RunMods
                var modsConfig = mods.MissionMapConfig;
                if (modsConfig != null)
                {
                    int len = modsConfig.LevelConfigs?.Length ?? 0;
                    Log.LogInfo($"[Apply] RunMods.MissionMapConfig: LevelConfigs={len}, name={modsConfig.name}");
                    ExpandMissionMapConfig(modsConfig, "[Apply] RunMods", targetStages);
                }
                else
                {
                    Log.LogWarning("[Apply] RunMods.MissionMapConfig is NULL - trying RSM");
                }

                // Also expand whatever RunSettingsManager currently has
                try
                {
                    var rsmConfig = runSettingsManager.GetMissionMapConfig();
                    if (rsmConfig != null)
                    {
                        int rsmLen = rsmConfig.LevelConfigs?.Length ?? 0;
                        Log.LogInfo($"[Apply] RSM.GetMissionMapConfig(): LevelConfigs={rsmLen}, name={rsmConfig.name}");
                        ExpandMissionMapConfig(rsmConfig, "[Apply] RSM", targetStages);
                    }
                }
                catch { }

                // Expand ALL 5 biome configs on the weapon slots directly
                ExpandMissionMapConfig(__instance.CC_MissionMapConfig, $"[Apply] {weaponName} CC", targetStages);
                ExpandMissionMapConfig(__instance.MC_MissionMapConfig, $"[Apply] {weaponName} MC", targetStages);
                ExpandMissionMapConfig(__instance.HB_MissionMapConfig, $"[Apply] {weaponName} HB", targetStages);
                ExpandMissionMapConfig(__instance.SP_MissionMapConfig, $"[Apply] {weaponName} SP", targetStages);
                ExpandMissionMapConfig(__instance.AW_MissionMapConfig, $"[Apply] {weaponName} AW", targetStages);
            }
            catch (Exception ex)
            {
                Log.LogError($"[Apply] Error: {ex.Message}");
                Log.LogError(ex.StackTrace);
            }
        }

        // ============================================================
        // Hook 3: Intercept GetMissionMapConfig at runtime - expand if needed
        // ============================================================
        [HarmonyPatch(typeof(RunSettingsManager), "GetMissionMapConfig")]
        [HarmonyPostfix]
        private static void GetMissionMapConfigPostfix(ref MissionMapConfig __result)
        {
            if (!_enableMod.Value || !_inWeaponMasteryRun || __result == null) return;
            try
            {
                int len = __result.LevelConfigs?.Length ?? 0;
                int targetStages = _targetStages.Value;
                if (len > 0 && len < targetStages)
                {
                    Log.LogInfo($"[GetMissionMapConfig] config has {len} stages, expanding");
                    ExpandMissionMapConfig(__result, "[GetMissionMapConfig]", targetStages);
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[GetMissionMapConfig] Error: {ex.Message}");
            }
        }

        // ============================================================
        // Hook 4: Intercept PrepNewRun to expand after run setup
        // ============================================================
        [HarmonyPatch(typeof(RunSettingsManager), "PrepNewRun")]
        [HarmonyPostfix]
        private static void PrepNewRunPostfix(RunSettingsManager __instance, EMissionType missionType)
        {
            if (!_enableMod.Value || !_inWeaponMasteryRun) return;
            try
            {
                Log.LogInfo($"[PrepNewRun] missionType={missionType}");
                var config = __instance.GetMissionMapConfig();
                if (config != null)
                {
                    int len = config.LevelConfigs?.Length ?? 0;
                    Log.LogInfo($"[PrepNewRun] MissionMapConfig: LevelConfigs={len}, name={config.name}");
                    ExpandMissionMapConfig(config, "[PrepNewRun]", _targetStages.Value);
                }

                // Also check LevelConfigs property directly
                var levelConfigs = __instance.LevelConfigs;
                Log.LogInfo($"[PrepNewRun] RSM.LevelConfigs.Length={levelConfigs?.Length ?? -1}");
            }
            catch (Exception ex)
            {
                Log.LogError($"[PrepNewRun] Error: {ex.Message}");
                Log.LogError(ex.StackTrace);
            }
        }

        // ============================================================
        // Hook 5: Override IsLastLevel to allow more stages
        // ============================================================
        [HarmonyPatch(typeof(RunStateManager), "IsLastLevel")]
        [HarmonyPostfix]
        private static void IsLastLevelPostfix(RunStateManager __instance, ref bool __result)
        {
            if (!_enableMod.Value || !_inWeaponMasteryRun) return;
            try
            {
                int currentStage = __instance.CurrentStageIndex;
                int targetStages = _targetStages.Value;
                bool originalResult = __result;
                if (currentStage < targetStages - 1)
                {
                    __result = false;
                }
                if (originalResult != __result)
                {
                    Log.LogInfo($"[IsLastLevel] stage={currentStage}, override {originalResult}->{__result} (target={targetStages})");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[IsLastLevel] Error: {ex.Message}");
            }
        }

        // ============================================================
        // Hook 6: Log run start state
        // ============================================================
        [HarmonyPatch(typeof(RunStateManager), "OnCoreRunStarted")]
        [HarmonyPostfix]
        private static void OnCoreRunStartedPostfix(RunStateManager __instance)
        {
            if (!_enableMod.Value) return;
            try
            {
                int stageLen = __instance.StageLength;
                int currentStage = __instance.CurrentStageIndex;
                Log.LogInfo($"[OnCoreRunStarted] StageLength={stageLen}, CurrentStage={currentStage}, weaponMastery={_inWeaponMasteryRun}");
            }
            catch (Exception ex)
            {
                Log.LogError($"[OnCoreRunStarted] Error: {ex.Message}");
            }
        }

        // ============================================================
        // Hook 7: Override StageLength property to return target stages
        // ============================================================
        [HarmonyPatch(typeof(RunStateManager), "get_StageLength")]
        [HarmonyPostfix]
        private static void StageLengthPostfix(ref int __result)
        {
            if (!_enableMod.Value || !_inWeaponMasteryRun) return;
            try
            {
                int targetStages = _targetStages.Value;
                if (__result > 0 && __result < targetStages)
                {
                    Log.LogInfo($"[get_StageLength] overriding {__result} -> {targetStages}");
                    __result = targetStages;
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[get_StageLength] Error: {ex.Message}");
            }
        }

        // ============================================================
        // Shared expansion method
        // ============================================================
        private static bool ExpandMissionMapConfig(MissionMapConfig config, string label, int targetStages)
        {
            if (config == null) return false;

            var levels = config.LevelConfigs;
            int currentLen = levels?.Length ?? 0;

            if (currentLen == 0 || currentLen >= targetStages)
                return false;

            // Clone the last LevelConfig for new stages
            var lastLevel = levels[currentLen - 1];
            var newLevels = new Il2CppReferenceArray<LevelConfig>(targetStages);

            for (int i = 0; i < currentLen; i++)
                newLevels[i] = levels[i];

            for (int i = currentLen; i < targetStages; i++)
            {
                if (lastLevel != null)
                {
                    var clone = UnityEngine.Object.Instantiate(lastLevel);
                    clone.name = $"{lastLevel.name}_stage{i + 1}";
                    newLevels[i] = clone;
                }
            }

            config.LevelConfigs = newLevels;

            // Also expand bossMissions if needed
            var bosses = config.bossMissions;
            int bossLen = bosses?.Length ?? 0;
            if (bossLen > 0 && bossLen < targetStages)
            {
                var lastBoss = bosses[bossLen - 1];
                var newBosses = new Il2CppReferenceArray<BossMissionPool>(targetStages);
                for (int i = 0; i < bossLen; i++)
                    newBosses[i] = bosses[i];
                for (int i = bossLen; i < targetStages; i++)
                    newBosses[i] = lastBoss;
                config.bossMissions = newBosses;
            }

            int verifyLen = config.LevelConfigs?.Length ?? -1;
            Log.LogInfo($"  {label}: Expanded {currentLen} -> {verifyLen}");
            return true;
        }
    }
}
