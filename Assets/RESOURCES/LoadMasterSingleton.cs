using UnityEngine;

public class LoadMasterSingleton : MonoBehaviour
{
    // Runs before a scene gets loaded
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void LoadMain()
    {
        //Debug.Log("BEFORE SCENE LOADED CALLED");
        GameObject main = Instantiate(Resources.Load("MasterSingleton")) as GameObject;
        DontDestroyOnLoad(main);
    }
    // You can choose to add any "Service" component to the Main prefab.
    // Examples are: Input, Saving, Sound, Config, Asset Bundles, Advertisements
}