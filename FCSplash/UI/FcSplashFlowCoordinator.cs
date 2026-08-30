using BeatSaberMarkupLanguage;
using HMUI;
using Zenject;

namespace FCSplash.UI;

public class FcSplashFlowCoordinator : FlowCoordinator
{
    private FcSplashSettingsViewController _settingsViewController = null!;
    private FcImageAudioViewController _fcImageAudioViewController = null!;
    private EmptyViewController _emptyViewController = null!;

    [Inject]
    public void Construct(FcSplashSettingsViewController settingsViewController, FcImageAudioViewController imageAudioViewController,  EmptyViewController emptyViewController)
    {
        _settingsViewController = settingsViewController;
        _fcImageAudioViewController = imageAudioViewController;
        _emptyViewController = emptyViewController;
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        if (firstActivation)
        {
            showBackButton = false;
            ProvideInitialViewControllers(_emptyViewController, _settingsViewController,_fcImageAudioViewController);
        }
    }
    
    public void DismissSelf()
    {
        BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }

    protected override void BackButtonWasPressed(ViewController topViewController)
    {
        BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
    }
}