using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Planora.Application.Abstractions.Tasks;
using Planora.Application.Common.Tasks;

namespace Planora.Infrastructure.Tasks;

public sealed class LocalTaskAttachmentStorage : ITaskAttachmentStorage
{
    private readonly string _storageRoot;

    public LocalTaskAttachmentStorage(
        IHostEnvironment environment,
        IOptions<TaskAttachmentOptions> options)
    {
        string configuredPath = options.Value.StoragePath;
        _storageRoot = Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath));
    }

    public async Task<string> SaveAsync(
        Stream content,
        string extension,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storageRoot);
        string storageKey = $"{Guid.NewGuid():N}{extension}";
        string fullPath = ResolvePath(storageKey);

        try
        {
            await using (var destination = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(destination, cancellationToken);
            }
            return storageKey;
        }
        catch
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        string fullPath = ResolvePath(storageKey);
        Stream? stream = File.Exists(fullPath)
            ? new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        string fullPath = ResolvePath(storageKey);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public async Task RestoreAsync(
        string storageKey,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storageRoot);
        string fullPath = ResolvePath(storageKey);
        try
        {
            await using (var destination = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(destination, cancellationToken);
            }
        }
        catch
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            throw;
        }
    }

    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            storageKey != Path.GetFileName(storageKey))
            throw new InvalidOperationException("The attachment storage key is invalid.");

        string fullPath = Path.GetFullPath(Path.Combine(_storageRoot, storageKey));
        string rootPrefix = _storageRoot.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The attachment storage key is invalid.");
        return fullPath;
    }
}
