
namespace Redpoint.KubernetesManager.PxeBoot.Bootmgr
{
    public interface IEfiBootManagerParser
    {
        EfiBootManagerConfiguration ParseBootManagerConfiguration(string efibootmgrOutput);
    }
}
