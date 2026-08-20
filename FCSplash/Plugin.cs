using HarmonyLib;
using IPA;
using SiraUtil.Zenject;
using IPALogger = IPA.Logging.Logger;

namespace FCSplash;

[Plugin(RuntimeOptions.SingleStartInit)]
public class Plugin
{
    internal static Plugin Instance { get; private set; } = null!;
    internal static IPALogger Log { get; private set; } = null!;

    private Harmony? _harmony;

    [Init]
    public void Init(IPALogger logger, Zenjector zenjector)
    {
        Instance = this;
        Log = logger;

        zenjector.UseLogger(logger);
        zenjector.Install<FcSpawnInstaller>(Location.Player);
    }

    [OnEnable]
    public void OnEnable()
    {
        _harmony = new Harmony("com.fcsplash.mod");
        _harmony.PatchAll();
        Log.Info("FCSplash initialized");
    }

    [OnDisable]
    public void OnDisable()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}
