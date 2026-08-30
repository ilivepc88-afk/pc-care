using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PcCare.Core.Models;

namespace PcCare.Windows.Services;

internal sealed class StartupItemMetadataReader
{
    public void Populate(StartupItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ExecutablePath) ||
            !Path.IsPathFullyQualified(item.ExecutablePath))
        {
            item.FileExists = false;
            return;
        }

        try
        {
            var file = new FileInfo(item.ExecutablePath);
            item.FileExists = file.Exists;
            if (!file.Exists)
            {
                return;
            }

            item.FileSize = file.Length;
            FileVersionInfo version = FileVersionInfo.GetVersionInfo(file.FullName);
            item.Description = FirstNonEmpty(item.Description, version.FileDescription);
            item.ProductName = version.ProductName ?? string.Empty;
            item.CompanyName = version.CompanyName ?? string.Empty;
            item.Publisher = FirstNonEmpty(item.Publisher, item.CompanyName);
            item.FileVersion = version.FileVersion ?? version.ProductVersion ?? string.Empty;
            item.SignatureStatus = ReadSignatureStatus(file.FullName);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or ArgumentException)
        {
            item.FileExists = false;
            item.SignatureStatus = DigitalSignatureStatus.Unknown;
        }
    }

    private static DigitalSignatureStatus ReadSignatureStatus(string path)
    {
        try
        {
#pragma warning disable SYSLIB0057
            using X509Certificate2 certificate = X509Certificate2.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            return string.IsNullOrWhiteSpace(certificate.Subject)
                ? DigitalSignatureStatus.Unknown
                : DigitalSignatureStatus.Signed;
        }
        catch (CryptographicException)
        {
            return DigitalSignatureStatus.Unsigned;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return DigitalSignatureStatus.Unknown;
        }
    }

    private static string FirstNonEmpty(string first, string? second)
    {
        return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
    }
}
