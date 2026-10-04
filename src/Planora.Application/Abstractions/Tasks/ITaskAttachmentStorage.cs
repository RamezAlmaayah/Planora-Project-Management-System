namespace Planora.Application.Abstractions.Tasks;

public interface ITaskAttachmentStorage
{
    Task<string> SaveAsync(
        Stream content,
        string extension,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        string storageKey,
        Stream content,
        CancellationToken cancellationToken = default);
}
