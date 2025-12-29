using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    private static readonly object lockObj = new object();
    private static bool applicationIsQuitting = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        applicationIsQuitting = false;
    }
    public static T Instance
    {
        get
        {
            if (applicationIsQuitting)
                return null;

            lock (lockObj)
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<T>();

                    if (FindObjectsByType<T>(FindObjectsSortMode.None).Length > 1)
                        return instance;

                    if (instance == null)
                    {
                        GameObject singletonObject = new(typeof(T).ToString() + " (Singleton)");
                        instance = singletonObject.AddComponent<T>();
                        DontDestroyOnLoad(singletonObject);
                    }
                }
                return instance;
            }
        }
    }

    public virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnApplicationQuit()
    {
        applicationIsQuitting = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}


