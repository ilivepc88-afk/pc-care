using System.Security;
using PcCare.Core.Models;

namespace PcCare.Windows.Services;

public sealed class BackgroundOptimizationManager
{
    private readonly BackgroundOptimizationService _service;

    public BackgroundOptimizationManager()
        : this(new BackgroundOptimizationService())
    {
    }

    internal BackgroundOptimizationManager(BackgroundOptimizationService service)
    {
        _service = service;
    }

    public Task<OptimizationOperationResult> ApplyAsync(OptimizationOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return Task.Run(() => Apply(operation, cancellationToken), cancellationToken);
    }

    private OptimizationOperationResult Apply(OptimizationOperation operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BackgroundOptimizationRule? rule = BackgroundOptimizationCatalog.Find(operation.ItemId);
        if (rule is null)
        {
            return Failure(operation, "优化项不在允许范围内。 ");
        }

        try
        {
            OptimizationItem item = _service.Evaluate(rule);
            if (!item.Supported)
            {
                return Failure(operation, "当前系统不支持该优化项。 ");
            }

            if (item.IsOrganizationManaged)
            {
                return Failure(operation, "该设置由组织策略管理，PcCare 不会覆盖。 ");
            }

            if (operation.Action == OptimizationAction.Apply)
            {
                foreach (RegistryValueLocation target in rule.Targets)
                {
                    _service.Registry.SetDword(target, rule.OptimizedValue);
                }

                _service.OwnershipStore.MarkOwned(rule.Id);
                return Success(operation, "已应用优化；未卸载任何组件或浏览器。 ");
            }

            if (!_service.OwnershipStore.IsOwned(rule.Id))
            {
                return Failure(operation, "该设置不是由 PcCare 创建，不能擅自删除现有策略。 ");
            }

            foreach (RegistryValueLocation target in rule.Targets)
            {
                _service.Registry.DeleteValue(target);
            }

            _service.OwnershipStore.ClearOwnership(rule.Id);
            return Success(operation, "已恢复为 Windows 默认的未配置状态。 ");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException or ArgumentException)
        {
            return Failure(operation, $"操作未完成：{exception.Message}");
        }
    }

    private static OptimizationOperationResult Success(OptimizationOperation operation, string message) => new(operation.ItemId, operation.Action, true, message.Trim());

    private static OptimizationOperationResult Failure(OptimizationOperation operation, string message) => new(operation.ItemId, operation.Action, false, message.Trim());
}
