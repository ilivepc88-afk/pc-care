using System.Text;
using System.Text.Json;
using PcCare.Core.Models;

namespace PcCare.App.Services;

internal static class StartupOperationCommandLine
{
    private const string OperationArgument = "--startup-operation";

    public static bool TryParse(string[] arguments, out StartupOperation? operation)
    {
        operation = null;
        if (arguments.Length != 2 || !string.Equals(arguments[0], OperationArgument, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            string json = Encoding.UTF8.GetString(Convert.FromBase64String(arguments[1]));
            StartupOperation? parsed = JsonSerializer.Deserialize<StartupOperation>(json);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.SourcePath) || string.IsNullOrWhiteSpace(parsed.OperationIdentity))
            {
                return false;
            }

            operation = parsed;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or DecoderFallbackException)
        {
            return false;
        }
    }

    public static string CreateArguments(StartupOperation operation)
    {
        string json = JsonSerializer.Serialize(operation);
        return $"{OperationArgument} {Convert.ToBase64String(Encoding.UTF8.GetBytes(json))}";
    }
}
