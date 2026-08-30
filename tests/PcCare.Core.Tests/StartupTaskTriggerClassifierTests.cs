using PcCare.Core.Services;

namespace PcCare.Core.Tests;

public sealed class StartupTaskTriggerClassifierTests
{
    [Theory]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(7, false)]
    public void IsLoginOrStartupTrigger_ClassifiesOnlyBootAndLogon(int triggerType, bool expected)
    {
        Assert.Equal(expected, StartupTaskTriggerClassifier.IsLoginOrStartupTrigger(triggerType));
    }
}
