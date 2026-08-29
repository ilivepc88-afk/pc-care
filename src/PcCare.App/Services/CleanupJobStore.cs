using System.Text.Json;
using PcCare.Core.Models;
using PcCare.Windows.Services;

namespace PcCare.App.Services;

public sealed class CleanupJobStore(OutputDirectoryResolver outputDirectoryResolver)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly OutputDirectoryResolver _outputDirectoryResolver = outputDirectoryResolver ?? throw new ArgumentNullException(nameof(outputDirectoryResolver));

    public string GetResultPath(Guid jobId)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("任务编号不能为空。", nameof(jobId));
        }

        return Path.Combine(_outputDirectoryResolver.ResolveJobsDirectory(), $"{jobId:D}.result.json");
    }

    public async Task WriteResultAsync(Guid jobId, CleanupExecutionResult result, CancellationToken cancellationToken = default)
    {
        string resultPath = GetResultPath(jobId);
        await using FileStream stream = new(
            resultPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CleanupExecutionResult> ReadAndDeleteResultAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        string resultPath = GetResultPath(jobId);
        try
        {
            await using FileStream stream = new(
                resultPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous);
            CleanupExecutionResult? result = await JsonSerializer.DeserializeAsync<CleanupExecutionResult>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return result ?? throw new InvalidDataException("提权清理进程没有返回有效结果。");
        }
        finally
        {
            try
            {
                File.Delete(resultPath);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                // A stale result is harmless and will be overwritten only by a new unique job id.
            }
        }
    }
}
