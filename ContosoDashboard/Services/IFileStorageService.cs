namespace ContosoDashboard.Services;

public sealed record StagedFile(string Key, string PhysicalPath, long Length);

public interface IFileStorageService
{
    Task<StagedFile> StageAsync(
        Stream content,
        long maximumBytes = long.MaxValue,
        CancellationToken cancellationToken = default);

    Task<string> PublishAsync(StagedFile stagedFile, string extension, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string relativeKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativeKey, CancellationToken cancellationToken = default);
}