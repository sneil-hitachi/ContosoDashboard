using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public sealed class LocalFileStorageServiceTests
{
    [Fact]
    public async Task StagesAndPublishesWithDistinctGeneratedKeys()
    {
        await using var temporaryStorage = new TemporaryFileStorage();
        var storage = new LocalFileStorageService(temporaryStorage.RootPath);

        var firstStaged = await storage.StageAsync(new MemoryStream([1, 2, 3]));
        var secondStaged = await storage.StageAsync(new MemoryStream([4, 5, 6]));
        var firstKey = await storage.PublishAsync(firstStaged, ".pdf");
        var secondKey = await storage.PublishAsync(secondStaged, ".pdf");

        Assert.NotEqual(firstKey, secondKey);
        Assert.StartsWith("files/", firstKey, StringComparison.Ordinal);
        Assert.StartsWith("staging/", firstStaged.Key, StringComparison.Ordinal);
        Assert.False(File.Exists(firstStaged.PhysicalPath));
        Assert.True(File.Exists(Path.Combine(temporaryStorage.RootPath, firstKey.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task RejectsTraversalKeysAndSupportsCleanupAndReadback()
    {
        await using var temporaryStorage = new TemporaryFileStorage();
        var storage = new LocalFileStorageService(temporaryStorage.RootPath);
        var staged = await storage.StageAsync(new MemoryStream([7, 8, 9]));
        var acceptedKey = await storage.PublishAsync(staged, ".txt");

        await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync("../secret.txt"));
        using (var content = await storage.OpenReadAsync(acceptedKey))
        using (var copy = new MemoryStream())
        {
            await content!.CopyToAsync(copy);
            Assert.Equal(new byte[] { 7, 8, 9 }, copy.ToArray());
        }

        await storage.DeleteAsync(acceptedKey);
        Assert.False(File.Exists(Path.Combine(temporaryStorage.RootPath, acceptedKey.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void RootIsPrivateAndOutsideWebRoot()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), "ContosoDashboard", "wwwroot");
        var storage = new LocalFileStorageService(Path.Combine(Path.GetTempPath(), "ContosoDashboardPrivate", Guid.NewGuid().ToString("N")));

        Assert.False(Path.GetFullPath(storage.RootPath).StartsWith(Path.GetFullPath(webRoot), StringComparison.OrdinalIgnoreCase));
    }
}