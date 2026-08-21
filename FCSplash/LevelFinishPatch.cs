using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace FCSplash
{
    [HarmonyPatch(typeof(StandardLevelScenesTransitionSetupDataSO), nameof(StandardLevelScenesTransitionSetupDataSO.Finish))]
    public class LevelFinishPatch
    {
        private static bool _isWaiting = false;

        public static bool Prefix(StandardLevelScenesTransitionSetupDataSO __instance, LevelCompletionResults levelCompletionResults)
        {
            if (_isWaiting) return true;

            Plugin.Log.Info($"LevelFinishPatch: Level ended with state -> {levelCompletionResults.levelEndStateType}");

            if (levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
            {
                Plugin.Log.Info("LevelFinishPatch: Level Cleared. Waiting 1 second for FC animation...");
                CoroutineHost.Start(DelayedFinishRoutine(__instance, levelCompletionResults));
                return false; 
            }

            return true;
        }
        
        private static IEnumerator DelayedFinishRoutine(StandardLevelScenesTransitionSetupDataSO instance, LevelCompletionResults results)
        {
            yield return new WaitForSeconds(1.25f);
            
            _isWaiting = true;
            instance.Finish(results);
            _isWaiting = false;
        }
    }
}