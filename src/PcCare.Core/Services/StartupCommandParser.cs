using System.Text.RegularExpressions;

namespace PcCare.Core.Services;

public sealed record StartupCommand(string ExecutablePath, string Arguments);

public static partial class StartupCommandParser
{
    public static StartupCommand Parse(string? command)
    {
        string expanded = Environment.ExpandEnvironmentVariables(command?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(expanded))
        {
            return new StartupCommand(string.Empty, string.Empty);
        }

        string normalized = TrimRunOncePrefixes(expanded);
        if (normalized.StartsWith('"'))
        {
            int endQuote = normalized.IndexOf('"', 1);
            if (endQuote > 0)
            {
                return new StartupCommand(
                    normalized[1..endQuote],
                    normalized[(endQuote + 1)..].Trim());
            }
        }

        Match executableMatch = ExecutablePattern().Match(normalized);
        if (executableMatch.Success)
        {
            string executable = executableMatch.Value.Trim().Trim('"');
            return new StartupCommand(executable, normalized[executableMatch.Length..].Trim());
        }

        int separator = normalized.IndexOfAny([' ', '\t']);
        return separator < 0
            ? new StartupCommand(normalized, string.Empty)
            : new StartupCommand(normalized[..separator], normalized[(separator + 1)..].Trim());
    }

    private static string TrimRunOncePrefixes(string command)
    {
        int index = 0;
        while (index < command.Length && (command[index] == '!' || command[index] == '*'))
        {
            index++;
        }

        return command[index..].TrimStart();
    }

    [GeneratedRegex(@"(?i)^.+?\.(exe|com|bat|cmd|vbs|js|ps1|lnk)(?=\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex ExecutablePattern();
}
