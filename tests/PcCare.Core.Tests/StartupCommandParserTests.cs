using PcCare.Core.Services;

namespace PcCare.Core.Tests;

public sealed class StartupCommandParserTests
{
    [Fact]
    public void Parse_QuotedExecutable_KeepsArguments()
    {
        StartupCommand command = StartupCommandParser.Parse("\"C:\\Program Files\\Contoso\\agent.exe\" --silent /login");

        Assert.Equal(@"C:\Program Files\Contoso\agent.exe", command.ExecutablePath);
        Assert.Equal("--silent /login", command.Arguments);
    }

    [Fact]
    public void Parse_RunOncePrefixAndUnquotedPath_ReadsExecutable()
    {
        StartupCommand command = StartupCommandParser.Parse("!*C:\\Tools\\updater.exe /background");

        Assert.Equal(@"C:\Tools\updater.exe", command.ExecutablePath);
        Assert.Equal("/background", command.Arguments);
    }

    [Fact]
    public void Parse_EnvironmentVariable_ExpandsBeforeParsing()
    {
        const string variableName = "PCCARE_TEST_STARTUP_ROOT";
        string? originalValue = Environment.GetEnvironmentVariable(variableName);
        try
        {
            Environment.SetEnvironmentVariable(variableName, @"C:\Program Files\PcCare Test");
            StartupCommand command = StartupCommandParser.Parse("%PCCARE_TEST_STARTUP_ROOT%\\tool.exe /a");

            Assert.Equal(@"C:\Program Files\PcCare Test\tool.exe", command.ExecutablePath);
            Assert.Equal("/a", command.Arguments);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }
}
