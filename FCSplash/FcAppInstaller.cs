using Zenject;

namespace FCSplash;

public class FcAppInstaller : Installer
{
    public override void InstallBindings()
    {
        Container.BindInterfacesAndSelfTo<FcAssetPreloader>().AsSingle().NonLazy();
    }
}