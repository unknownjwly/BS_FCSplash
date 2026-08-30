using Zenject;
using FCSplash.Features.Tracking;

namespace FCSplash;

public class FcInstaller : Installer
{
    public override void InstallBindings()
    {
        Container.BindInterfacesAndSelfTo<FcTrackerManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<FcAssetPreloader>().AsSingle().NonLazy();
    }
}