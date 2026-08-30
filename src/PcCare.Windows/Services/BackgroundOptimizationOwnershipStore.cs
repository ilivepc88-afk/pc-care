using Microsoft.Win32;

namespace PcCare.Windows.Services;

internal sealed class BackgroundOptimizationOwnershipStore
{
    private const string ManagedPath = @"Software\PcCare\BackgroundOptimization\Managed";
    private readonly RegistryManager _registry;

    public BackgroundOptimizationOwnershipStore(RegistryManager registry)
    {
        _registry = registry;
    }

    public bool IsOwned(string itemId) => _registry.Read(Marker(itemId)).DwordValue == 1;

    public void MarkOwned(string itemId) => _registry.SetDword(Marker(itemId), 1);

    public void ClearOwnership(string itemId) => _registry.DeleteValue(Marker(itemId));

    private static RegistryValueLocation Marker(string itemId) => new(RegistryHive.CurrentUser, ManagedPath, itemId);
}
