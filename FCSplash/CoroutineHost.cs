using System.Collections;
using UnityEngine;

namespace FCSplash
{
    public class CoroutineHost : MonoBehaviour
    {
        private static CoroutineHost? _instance;

        public static void Start(IEnumerator routine)
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("FCSplashCoroutineHost");
                DontDestroyOnLoad(obj);
                _instance = obj.AddComponent<CoroutineHost>();
            }

            _instance.StartCoroutine(_instance.RunRoutine(routine));
        }

        private IEnumerator RunRoutine(IEnumerator routine)
        {
            yield return StartCoroutine(routine);

            if (transform.childCount == 0)
            {
                Destroy(gameObject);
                _instance = null;
            }
        }
    }
}