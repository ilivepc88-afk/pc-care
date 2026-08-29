namespace PcCare.Windows.Services;

public sealed class OutputDirectoryResolver
{
    public string ResolveReportsDirectory()
    {
        string applicationDirectory = AppContext.BaseDirectory;
        string preferred = Path.Combine(applicationDirectory, "Reports");

        if (CanWrite(preferred))
        {
            return preferred;
        }

        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string fallback = Path.Combine(documents, "PcCare", "Reports");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    public string ResolveJobsDirectory()
    {
        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string jobs = Path.Combine(localApplicationData, "PcCare", "Jobs");
        Directory.CreateDirectory(jobs);
        return jobs;
    }

    private static bool CanWrite(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, $".{Guid.NewGuid():N}.probe");
            using (File.Create(probe))
            {
            }

            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
