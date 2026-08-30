using PcCare.Core.Models;

namespace PcCare.Core.Services;

public sealed record OptimizationEvaluation(
    OptimizationState State,
    bool CanOptimize,
    bool CanRestore,
    bool IsOrganizationManaged);

public static class OptimizationStateEvaluator
{
    public static OptimizationEvaluation Evaluate(
        bool supported,
        bool isPolicy,
        bool anyConfigured,
        bool optimized,
        bool ownedByTool,
        bool treatMissingAsOptimized = false)
    {
        if (!supported)
        {
            return new OptimizationEvaluation(OptimizationState.Unsupported, false, false, false);
        }

        if (isPolicy && anyConfigured && !ownedByTool)
        {
            return new OptimizationEvaluation(OptimizationState.OrganizationManaged, false, false, true);
        }

        if (optimized || treatMissingAsOptimized)
        {
            return new OptimizationEvaluation(OptimizationState.Disabled, false, ownedByTool && optimized, false);
        }

        OptimizationState state = anyConfigured ? OptimizationState.Enabled : OptimizationState.NotConfigured;
        return new OptimizationEvaluation(state, true, false, false);
    }
}
