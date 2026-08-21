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

            if (levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared)
            {
                CoroutineHost.Start(DelayedFinishRoutine(__instance, levelCompletionResults));
                return false; 
            }

            return true;
        }
        
        private static IEnumerator DelayedFinishRoutine(StandardLevelScenesTransitionSetupDataSO instance, LevelCompletionResults results)
        {
            yield return new WaitForSeconds(1.75f);
            
            _isWaiting = true;
            instance.Finish(results);
            _isWaiting = false;
        }
    }
}
