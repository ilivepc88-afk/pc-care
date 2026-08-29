namespace PcCare.App.Services;

public sealed class CommandLineOptions
{
    public bool IsElevatedCleanup { get; private init; }

    public Guid JobId { get; private init; }

    public IReadOnlyList<string> CategoryIds { get; private init; } = [];

    public static CommandLineOptions Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        bool elevated = arguments.Contains("--elevated-clean", StringComparer.OrdinalIgnoreCase);
        Guid jobId = Guid.Empty;
        var categories = new List<string>();

        for (int index = 0; index < arguments.Count; index++)
        {
            if (arguments[index].Equals("--job", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < arguments.Count &&
                Guid.TryParse(arguments[index + 1], out Guid parsedJob))
            {
                jobId = parsedJob;
                index++;
                continue;
            }

            if (arguments[index].Equals("--categories", StringComparison.OrdinalIgnoreCase) && index + 1 < arguments.Count)
            {
                categories.AddRange(arguments[index + 1]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(IsSafeCategoryId));
                index++;
            }
        }

        return new CommandLineOptions
        {
            IsElevatedCleanup = elevated,
            JobId = jobId,
            CategoryIds = categories.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static bool IsSafeCategoryId(string value)
    {
        return value.Length is > 0 and <= 64 && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character == '-');
    }
}
