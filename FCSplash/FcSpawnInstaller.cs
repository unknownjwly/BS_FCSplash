using Zenject;

namespace FCSplash;

public class FcSpawnInstaller : Installer
{
    public override void InstallBindings()
    {
        Container.BindInterfacesAndSelfTo<FcSpawnManager>().AsSingle();
    }
}