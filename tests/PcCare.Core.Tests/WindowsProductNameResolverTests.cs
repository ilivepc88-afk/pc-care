using PcCare.Core.Services;

namespace PcCare.Core.Tests;

public sealed class WindowsProductNameResolverTests
{
    [Fact]
    public void Resolve_UsesBuildNumberToCorrectWindows11EnterpriseLtscName()
    {
        string result = WindowsProductNameResolver.Resolve("Windows 10 Enterprise LTSC 2024", "26100");

        Assert.Equal("Windows 11 Enterprise LTSC 2024", result);
    }

    [Fact]
    public void Resolve_PreservesPrefixAndEditionWhenCorrectingWindows11Name()
    {
        string result = WindowsProductNameResolver.Resolve("Microsoft Windows 10 Enterprise", "22631");

        Assert.Equal("Microsoft Windows 11 Enterprise", result);
    }

    [Fact]
    public void Resolve_DoesNotRelabelWindows10Build()
    {
        string result = WindowsProductNameResolver.Resolve("Windows 10 Enterprise LTSC 2021", "19044");

        Assert.Equal("Windows 10 Enterprise LTSC 2021", result);
    }

    [Fact]
    public void Resolve_ReturnsWindows11WhenProductNameIsMissing()
    {
        string result = WindowsProductNameResolver.Resolve(null, "26100");

        Assert.Equal("Windows 11", result);
    }

    [Fact]
    public void Resolve_PreservesProductNameWhenBuildIsInvalid()
    {
        string result = WindowsProductNameResolver.Resolve("Windows 10 Enterprise", "unknown");

        Assert.Equal("Windows 10 Enterprise", result);
    }
}
