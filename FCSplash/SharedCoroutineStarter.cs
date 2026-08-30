using UnityEngine;

namespace FCSplash;

public class SharedCoroutineStarter : MonoBehaviour
{
    private static SharedCoroutineStarter? _instance;

    public static SharedCoroutineStarter instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("FCSplash_SharedCoroutineStarter");
                _instance = obj.AddComponent<SharedCoroutineStarter>();
                DontDestroyOnLoad(obj);
            }
            return _instance;
        }
    }
}