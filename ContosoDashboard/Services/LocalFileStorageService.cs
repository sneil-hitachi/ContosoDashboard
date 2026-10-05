using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services;

public sealed class DocumentStorageOptions
{
    public string StorageRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ContosoDashboard",
        "Documents");
}

public sealed class DocumentFileTooLargeException : Exception
{
    public DocumentFileTooLargeException() : base("The file exceeds the permitted size.")
    {
    }
}

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly string _stagingPath;
    private readonly string _filesPath;

    public LocalFileStorageService(IOptions<DocumentStorageOptions> options)
        : this(options.Value.StorageRoot)
    {
    }

    public LocalFileStorageService(string storageRoot)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("A private storage root is required.", nameof(storageRoot));
        }

        _rootPath = Path.GetFullPath(storageRoot);
        if (Path.GetPathRoot(_rootPath) is null ||
            _rootPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => string.Equals(segment, "wwwroot", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Document storage must be outside wwwroot.", nameof(storageRoot));
        }

        _stagingPath = Path.Combine(_rootPath, "staging");
        _filesPath = Path.Combine(_rootPath, "files");
        Directory.CreateDirectory(_stagingPath);
        Directory.CreateDirectory(_filesPath);
    }

    public string RootPath => _rootPath;

    public async Task<StagedFile> StageAsync(
        Stream content,
        long maximumBytes = long.MaxValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var key = $"staging/{Guid.NewGuid():N}.stage";
        var path = ResolvePath(key, allowStaging: true);
        long length = 0;

        try
        {
            await using var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
            var buffer = new byte[64 * 1024];
            int bytesRead;
            while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) != 0)
            {
                length += bytesRead;
                if (length > maximumBytes)
                {
                    throw new DocumentFileTooLargeException();
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }
        catch
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            throw;
        }

        return new StagedFile(key, path, length);
    }

    public async Task<string> PublishAsync(StagedFile stagedFile, string extension, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(extension) || extension[0] != '.' ||
            extension.Length > 12 || extension.Skip(1).Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException("A validated file extension is required.", nameof(extension));
        }

        var sourcePath = ResolvePath(stagedFile.Key, allowStaging: true);
        var key = $"files/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var destinationPath = ResolvePath(key, allowStaging: false);
        File.Move(sourcePath, destinationPath);
        await Task.CompletedTask;
        return key;
    }

    public Task<Stream?> OpenReadAsync(string relativeKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(relativeKey, allowStaging: false);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 64 * 1024, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativeKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(relativeKey, allowStaging: true);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string relativeKey, bool allowStaging)
    {
        if (string.IsNullOrWhiteSpace(relativeKey) || Path.IsPathRooted(relativeKey))
        {
            throw new ArgumentException("The storage key is invalid.", nameof(relativeKey));
        }

        var normalizedKey = relativeKey.Replace('\\', '/');
        var allowedPrefix = allowStaging && normalizedKey.StartsWith("staging/", StringComparison.Ordinal)
            ? "staging/"
            : normalizedKey.StartsWith("files/", StringComparison.Ordinal)
                ? "files/"
                : null;
        if (allowedPrefix is null || normalizedKey.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("The storage key is invalid.", nameof(relativeKey));
        }

        var path = Path.GetFullPath(Path.Combine(_rootPath, normalizedKey.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The storage key is invalid.", nameof(relativeKey));
        }

        return path;
    }
}