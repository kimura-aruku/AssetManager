using AssetManager.Application.Paths;
using AssetManager.IntegrationTests.Persistence;
using AssetManager.Infrastructure.Windows;

namespace AssetManager.IntegrationTests.Paths;

public sealed class PhysicalWindowsPathFileSystemTests
{
    [Fact]
    public async Task PhysicalFileSystemDistinguishesAvailableAndMissingPaths()
    {
        using var temporary = new TemporaryDirectory();
        var filePath = temporary.GetPath("asset.txt");
        await File.WriteAllTextAsync(filePath, "content");
        var folderPath = temporary.GetPath("folder");
        _ = Directory.CreateDirectory(folderPath);
        var fileSystem = new PhysicalWindowsPathFileSystem();

        Assert.Equal(PathEntryKind.File, fileSystem.GetExistingKind(filePath));
        Assert.Equal(PathEntryKind.Folder, fileSystem.GetExistingKind(folderPath));
        Assert.Equal(PathCheckStatus.Available, fileSystem.Check(filePath).Status);
        Assert.Equal(PathCheckStatus.Available, fileSystem.Check(folderPath).Status);
        Assert.Equal(
            PathCheckStatus.Missing,
            fileSystem.Check(temporary.GetPath("missing.txt")).Status);
        Assert.Equal(DriveType.Fixed, fileSystem.GetDriveType(Path.GetPathRoot(filePath)!));
    }

    [Fact]
    public async Task RegistrationNormalizesAndValidatesPhysicalTargets()
    {
        using var temporary = new TemporaryDirectory();
        var filePath = temporary.GetPath("asset.txt");
        await File.WriteAllTextAsync(filePath, "content");
        var service = new PathRegistrationService(new PhysicalWindowsPathFileSystem());

        var result = service.RegisterTarget($"\"{filePath}\"");

        Assert.Equal(PathEntryKind.File.ToTargetPathKind(), result.Kind);
        Assert.Equal(WindowsPathNormalizer.NormalizeForStorage(filePath), result.Path);
    }
}
