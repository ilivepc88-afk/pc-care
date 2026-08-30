using PcCare.Core.Services;

namespace PcCare.Core.Tests;

public sealed class StartupApprovedStateTests
{
    [Theory]
    [InlineData((byte)0x02, true)]
    [InlineData((byte)0x06, true)]
    [InlineData((byte)0x08, true)]
    [InlineData((byte)0x01, false)]
    [InlineData((byte)0x03, false)]
    [InlineData((byte)0x07, false)]
    [InlineData((byte)0x09, false)]
    public void IsEnabled_ReadsKnownTaskManagerState(byte state, bool expected)
    {
        Assert.True(StartupApprovedState.IsKnown([state]));
        Assert.Equal(expected, StartupApprovedState.IsEnabled([state]));
    }

    [Fact]
    public void MissingOrUnknownState_DefaultsToEnabledWithoutClaimingCertainty()
    {
        Assert.False(StartupApprovedState.IsKnown(null));
        Assert.True(StartupApprovedState.IsEnabled(null));
        Assert.False(StartupApprovedState.IsKnown([0x55]));
        Assert.True(StartupApprovedState.IsEnabled([0x55]));
    }
}
