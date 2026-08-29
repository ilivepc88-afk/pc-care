using System.Globalization;

namespace PcCare.Core.Services;

public static class WindowsProductNameResolver
{
    private const int Windows11MinimumBuild = 22000;
    private const string Windows10Name = "Windows 10";
    private const string Windows11Name = "Windows 11";

    public static string Resolve(string? productName, string? currentBuildNumber)
    {
        string normalizedName = string.IsNullOrWhiteSpace(productName)
            ? "无法读取"
            : productName.Trim();

        if (!int.TryParse(currentBuildNumber, NumberStyles.None, CultureInfo.InvariantCulture, out int buildNumber) ||
            buildNumber < Windows11MinimumBuild)
        {
            return normalizedName;
        }

        int windows10Index = normalizedName.IndexOf(Windows10Name, StringComparison.OrdinalIgnoreCase);
        if (windows10Index >= 0)
        {
            return normalizedName[..windows10Index] +
                   Windows11Name +
                   normalizedName[(windows10Index + Windows10Name.Length)..];
        }

        return normalizedName == "无法读取" ? Windows11Name : normalizedName;
    }
}
