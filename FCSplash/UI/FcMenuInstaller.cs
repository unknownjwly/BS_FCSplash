using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using Zenject;

namespace FCSplash.UI;

public class FcMenuInstaller : Installer
{
    public override void InstallBindings()
    {
        Container.BindInterfacesAndSelfTo<FcMenuManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<FcImageAudioViewController>().FromNewComponentAsViewController().AsSingle();
        Container.Bind<FcSplashSettingsViewController>().FromNewComponentAsViewController().AsSingle();
        Container.Bind<EmptyViewController>().FromNewComponentAsViewController().AsSingle();
        Container.Bind<FcSplashFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
    }
}

public class FcMenuManager : IInitializable, System.IDisposable
{
    [Inject] private readonly FcSplashFlowCoordinator _splashFlowCoordinator = null!;
    private MenuButton? _menuButton;

    public void Initialize()
    {
        _menuButton = new MenuButton("FCSplash", "Configure your splash screen settings", ShowFlowCoordinator);
        MenuButtons.Instance.RegisterButton(_menuButton);
    }

    public void Dispose()
    {
        if (_menuButton != null && MenuButtons.Instance != null)
        {
            MenuButtons.Instance.UnregisterButton(_menuButton);
        }
    }

    private void ShowFlowCoordinator()
    {
        if (_splashFlowCoordinator == null)
        {
            Plugin.Log.Error("FcSplashFlowCoordinator is null!");
            return;
        }

        if (BeatSaberUI.MainFlowCoordinator == null)
        {
            Plugin.Log.Error("BeatSaberUI.MainFlowCoordinator is null!");
            return;
        }

        BeatSaberUI.MainFlowCoordinator.PresentFlowCoordinator(_splashFlowCoordinator);
    }
}