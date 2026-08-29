namespace PcCare.Core.Safety;

public static class PathSafety
{
    public static string NormalizeRoot(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        string normalized = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalized + Path.DirectorySeparatorChar;
    }

    public static bool IsWithinRoot(string candidatePath, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        string normalizedRoot = NormalizeRoot(rootPath);
        string normalizedCandidate = Path.GetFullPath(candidatePath);

        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsReparsePoint(FileSystemInfo fileSystemInfo)
    {
        ArgumentNullException.ThrowIfNull(fileSystemInfo);

        try
        {
            fileSystemInfo.Refresh();
            return (fileSystemInfo.Attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
