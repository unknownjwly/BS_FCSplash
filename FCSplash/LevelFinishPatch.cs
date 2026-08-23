using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FCSplash
{
    [HarmonyPatch(typeof(StandardLevelFinishedController), "StartLevelFinished")]
    public class LevelFinishPatch
    {
        private static bool _isDelayedRunning = false;
        
        private static readonly MethodInfo StartLevelFinishedMethod = 
            AccessTools.Method(typeof(StandardLevelFinishedController), "StartLevelFinished");

        public static bool Prefix(StandardLevelFinishedController __instance)
        {
            if (_isDelayedRunning)
            {
                return true; 
            }

            CoroutineHost.Start(DelayedLevelFinishRoutine(__instance));
            return false;
        }

        private static IEnumerator DelayedLevelFinishRoutine(StandardLevelFinishedController instance)
        {
            yield return new WaitForSeconds(Config.Instance.General.LevelFinishDelay);

            _isDelayedRunning = true;
            StartLevelFinishedMethod.Invoke(instance, null);
            _isDelayedRunning = false;
        }
    }
}
