using System.IO;
using System.Collections;
using HarmonyLib;
using IPA;
using IPA.Utilities;
using SiraUtil.Zenject;
using IPALogger = IPA.Logging.Logger;
using FCSplash.UI;
using FCSplash.Features;
using FCSplash.Features.Spawning;

namespace FCSplash;

[Plugin(RuntimeOptions.SingleStartInit)]
public class Plugin
{
    internal static Plugin Instance { get; private set; } = null!;
    internal static IPALogger Log { get; private set; } = null!;
    internal static SplashAudioManager AudioManager { get; private set; } = null!;

    private Harmony? _harmony;

    [Init]
    public void Init(IPALogger logger, Zenjector zenjector)
    {
        Instance = this;
        Log = logger;
        AudioManager = new SplashAudioManager();

        Config.Load();
        CreateRequiredFolders();

        zenjector.UseLogger(logger);
        zenjector.Install<FcInstaller>(Location.Player);
        zenjector.Install<FcMenuInstaller>(Location.Menu);
    }

    private void CreateRequiredFolders()
    {
        string userDataPath = UnityGame.UserDataPath;
        string modFolder = Path.Combine(userDataPath, "FCSplash");
        string imagesFolder = Path.Combine(modFolder, "Images & Gifs");
        string soundsFolder = Path.Combine(modFolder, "Sounds");
        
        Directory.CreateDirectory(imagesFolder);
        Directory.CreateDirectory(soundsFolder);
    }

    [OnEnable]
    public void OnEnable()
    {
        _harmony = new Harmony("com.fcsplash.mod");
        _harmony.PatchAll();
        
        BSMLHelper.ExtractAndEnsureCache("FcImageAudioViewController.bsml");
        
        SharedCoroutineStarter.instance.StartCoroutine(PreloadStartupAssetsRoutine());

        Log.Info("FCSplash initialized");
    }

    private IEnumerator PreloadStartupAssetsRoutine()
    {
        FcSpawner.InitializeOnStartup();
        yield return AudioManager.LoadAudioClipRoutine();
    }

    [OnDisable]
    public void OnDisable()
    {
        AudioManager.Cleanup();
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}
