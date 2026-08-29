using PcCare.Core.Safety;

namespace PcCare.Core.Tests;

public sealed class PathSafetyTests
{
    [Fact]
    public void IsWithinRoot_AcceptsChildAndRejectsSiblingPrefix()
    {
        using var temporary = new TemporaryDirectory();
        string child = System.IO.Path.Combine(temporary.Path, "child", "file.tmp");
        string sibling = temporary.Path + "-other" + System.IO.Path.DirectorySeparatorChar + "file.tmp";

        Assert.True(PathSafety.IsWithinRoot(child, temporary.Path));
        Assert.False(PathSafety.IsWithinRoot(sibling, temporary.Path));
    }

    [Fact]
    public void IsWithinRoot_RejectsParentTraversal()
    {
        using var temporary = new TemporaryDirectory();
        string escaped = System.IO.Path.Combine(temporary.Path, "folder", "..", "..", "outside.tmp");

        Assert.False(PathSafety.IsWithinRoot(escaped, temporary.Path));
    }
}
