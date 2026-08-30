using PcCare.Core.Models;
using PcCare.Core.Services;

namespace PcCare.Core.Tests;

public sealed class OptimizationStateEvaluatorTests
{
    [Theory]
    [InlineData(false, false, false, false, OptimizationState.Unsupported, false, false)]
    [InlineData(true, false, false, false, OptimizationState.NotConfigured, true, false)]
    [InlineData(true, false, true, false, OptimizationState.Enabled, true, false)]
    [InlineData(true, false, true, true, OptimizationState.Disabled, false, true)]
    [InlineData(true, true, true, false, OptimizationState.OrganizationManaged, false, false)]
    public void Evaluate_MapsFeatureAndRegistryStatesConservatively(
        bool supported,
        bool isPolicy,
        bool configured,
        bool optimized,
        OptimizationState expectedState,
        bool canOptimize,
        bool canRestore)
    {
        OptimizationEvaluation result = OptimizationStateEvaluator.Evaluate(
            supported,
            isPolicy,
            configured,
            optimized,
            ownedByTool: optimized);

        Assert.Equal(expectedState, result.State);
        Assert.Equal(canOptimize, result.CanOptimize);
        Assert.Equal(canRestore, result.CanRestore);
    }

    [Fact]
    public void Evaluate_LeavesExistingPolicyUntouchedWhenNotOwned()
    {
        OptimizationEvaluation result = OptimizationStateEvaluator.Evaluate(
            supported: true,
            isPolicy: true,
            anyConfigured: true,
            optimized: true,
            ownedByTool: false);

        Assert.True(result.IsOrganizationManaged);
        Assert.False(result.CanOptimize);
        Assert.False(result.CanRestore);
    }

    [Fact]
    public void Evaluate_TreatsLtscMissingConsumerExperienceAsAlreadyOptimized()
    {
        OptimizationEvaluation result = OptimizationStateEvaluator.Evaluate(
            supported: true,
            isPolicy: true,
            anyConfigured: false,
            optimized: false,
            ownedByTool: false,
            treatMissingAsOptimized: true);

        Assert.Equal(OptimizationState.Disabled, result.State);
        Assert.False(result.CanOptimize);
    }
}
