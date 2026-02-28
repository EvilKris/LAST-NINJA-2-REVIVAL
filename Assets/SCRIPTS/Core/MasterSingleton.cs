
public class MasterSingleton : Singleton<MasterSingleton>
{
    public static MasterSingleton Main { get; private set; }


    //Service Locators
    public CameraManager CameraManager { get; private set; }
    public PrefabBankManager PrefabBankManager { get; private set; }
    public UIManager UIManager { get; private set; }
    public AudioController AudioController { get; private set; }
    public GameDataManager GameDataManager { get; private set; }
    public PlayerManager PlayerManager { get; private set; }
    public InventoryManager InventoryManager { get; private set; }
    public EventManager EventManager { get; private set; }

    public override void Awake()
    {
        CameraManager = GetComponentInChildren<CameraManager>();
        PrefabBankManager = GetComponentInChildren<PrefabBankManager>();
        UIManager = GetComponentInChildren<UIManager>();
        AudioController = GetComponentInChildren<AudioController>();
        GameDataManager = GetComponentInChildren<GameDataManager>();
        PlayerManager = GetComponentInChildren<PlayerManager>(); //might not even need this, but just in case
        InventoryManager = GetComponentInChildren<InventoryManager>();
        EventManager = GetComponentInChildren<EventManager>();
    }

    
    private void OnDestroy()
    {
        //already supposedly dealt with in the Singleton class, but just in case
        if (Main) Main = null;
    }
}
