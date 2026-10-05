namespace ContosoDashboard.Tests.TestInfrastructure;

public sealed class TemporaryFileStorage : IAsyncDisposable
{
    public TemporaryFileStorage()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "ContosoDashboard.Tests", Guid.NewGuid().ToString("N"));
        StagingPath = Path.Combine(RootPath, "staging");
        AcceptedPath = Path.Combine(RootPath, "accepted");
        Directory.CreateDirectory(StagingPath);
        Directory.CreateDirectory(AcceptedPath);
    }

    public string RootPath { get; }
    public string StagingPath { get; }
    public string AcceptedPath { get; }

    public async Task<string> WriteStagedFileAsync(string fileName, byte[] content)
    {
        var path = Path.Combine(StagingPath, fileName);
        await File.WriteAllBytesAsync(path, content);
        return path;
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}