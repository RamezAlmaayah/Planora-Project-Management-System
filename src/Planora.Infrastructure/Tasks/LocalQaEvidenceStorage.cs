using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Planora.Application.Abstractions.Tasks;

namespace Planora.Infrastructure.Tasks;

public sealed class QaEvidenceStorageOptions
{
    public const string SectionName = "QaEvidenceStorage";
    public string StoragePath { get; set; } = "App_Data/QaEvidence";
}

public sealed class LocalQaEvidenceStorage : IQaEvidenceStorage
{
    private readonly string _root;

    public LocalQaEvidenceStorage(IHostEnvironment environment, IOptions<QaEvidenceStorageOptions> options)
    {
        _root = Path.GetFullPath(Path.IsPathRooted(options.Value.StoragePath)
            ? options.Value.StoragePath : Path.Combine(environment.ContentRootPath, options.Value.StoragePath));
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        string key = $"{Guid.NewGuid():N}{extension}";
        string path = Resolve(key);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await content.CopyToAsync(output, cancellationToken);
            return key;
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        string path = Resolve(storageKey);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        string path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key != Path.GetFileName(key))
            throw new InvalidOperationException("The QA evidence storage key is invalid.");
        string path = Path.GetFullPath(Path.Combine(_root, key));
        string prefix = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The QA evidence storage key is invalid.");
        return path;
    }
}
