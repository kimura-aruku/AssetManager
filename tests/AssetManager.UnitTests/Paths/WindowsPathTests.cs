using AssetManager.Application.Paths;
using AssetManager.Domain.Values;

namespace AssetManager.UnitTests.Paths;

public sealed class WindowsPathTests
{
    [Theory]
    [InlineData(@"c:/Assets/File.png", @"C:\Assets\File.png")]
    [InlineData("\"c:\\Assets\\File.png\"", @"C:\Assets\File.png")]
    [InlineData(@"\\?\c:\Assets\File.png", @"C:\Assets\File.png")]
    [InlineData(@"c:\", @"C:\")]
    public void NormalizeForStorageProducesCanonicalWindowsPath(string input, string expected)
    {
        Assert.Equal(expected, WindowsPathNormalizer.NormalizeForStorage(input));
    }

    [Fact]
    public void NormalizeForStoragePreservesValidSpaces()
    {
        const string path = @"C:\Assets\ file name .png";

        Assert.Equal(path, WindowsPathNormalizer.NormalizeForStorage(path));
    }

    [Theory]
    [InlineData(@"relative\file.png")]
    [InlineData("https://example.com/file.png")]
    [InlineData(@"\\server\share\file.png")]
    [InlineData("\"C:\\Assets\\file.png")]
    public void NormalizeForStorageRejectsUnsupportedInput(string input)
    {
        Assert.Throws<WindowsPathValidationException>(
            () => WindowsPathNormalizer.NormalizeForStorage(input));
    }

    [Fact]
    public void ComparisonKeyTreatsCaseSeparatorsAndExtendedPrefixAsEquivalent()
    {
        var first = WindowsPathNormalizer.CreateComparisonKey(@"c:/ASSETS/file.PNG\");
        var second = WindowsPathNormalizer.CreateComparisonKey(@"\\?\C:\assets\FILE.png");

        Assert.Equal(first, second, ignoreCase: true);
    }

    [Fact]
    public void TargetRegistrationRequiresFixedDriveAndExistingSinglePath()
    {
        var fileSystem = new FakeFileSystem
        {
            ExistingKinds = { [@"C:\Assets\file.png"] = PathEntryKind.File },
        };
        var service = new PathRegistrationService(fileSystem);

        var target = service.RegisterDroppedTarget([@"c:/Assets/file.png"]);

        Assert.Equal(TargetPathKind.File, target.Kind);
        Assert.Equal(@"C:\Assets\file.png", target.Path);
        Assert.Throws<WindowsPathValidationException>(
            () => service.RegisterDroppedTarget([@"C:\one", @"C:\two"]));
        Assert.Throws<WindowsPathValidationException>(
            () => service.RegisterTarget(@"C:\Assets\missing.png"));

        fileSystem.DriveType = DriveType.Removable;
        Assert.Throws<WindowsPathValidationException>(
            () => service.RegisterTarget(@"C:\Assets\file.png"));
    }

    [Fact]
    public void AuxiliaryPasteKeepsMissingPathAndImmediatelyChecksIt()
    {
        var fileSystem = new FakeFileSystem();
        var service = new PathRegistrationService(fileSystem);

        var registration = service.RegisterAuxiliary(
            @"C:\Assets\missing-license.txt",
            PathEntryKind.File);

        Assert.Equal(@"C:\Assets\missing-license.txt", registration.Path);
        Assert.Equal(PathCheckStatus.Missing, registration.CheckResult.Status);
        Assert.Equal(1, fileSystem.CheckCount);
    }

    [Fact]
    public void PickerResultUsesSameTargetValidationAndCancelReturnsNull()
    {
        var fileSystem = new FakeFileSystem
        {
            ExistingKinds = { [@"C:\Assets\file.png"] = PathEntryKind.File },
        };
        var picker = new FakePicker { FileResult = @"C:\Assets\file.png" };
        var service = new PathRegistrationService(fileSystem, picker);

        Assert.Equal(@"C:\Assets\file.png", service.PickTargetFile()!.Path);
        picker.FileResult = null;
        Assert.Null(service.PickTargetFile());
    }

    [Fact]
    public void AuxiliaryPickerReturnsNormalizedSelectedPath()
    {
        var fileSystem = new FakeFileSystem
        {
            ExistingKinds = { [@"C:\Assets\receipt.pdf"] = PathEntryKind.File },
        };
        var picker = new FakePicker { FileResult = @"c:/Assets/receipt.pdf" };
        var service = new PathRegistrationService(fileSystem, picker);

        var result = service.PickAuxiliaryFile("領収書を選択");

        Assert.NotNull(result);
        Assert.Equal(@"C:\Assets\receipt.pdf", result.Path);
        Assert.Equal(PathEntryKind.File, result.ExpectedKind);
    }

    private sealed class FakePicker : IWindowsPathPicker
    {
        public string? FileResult { get; set; }

        public string? FolderResult { get; set; }

        public string? PickFile(string title) => FileResult;

        public string? PickFolder(string title) => FolderResult;
    }

    private sealed class FakeFileSystem : IWindowsPathFileSystem
    {
        public Dictionary<string, PathEntryKind> ExistingKinds { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public DriveType DriveType { get; set; } = DriveType.Fixed;

        public int CheckCount { get; private set; }

        public DriveType GetDriveType(string driveRoot) => DriveType;

        public PathEntryKind? GetExistingKind(string path)
        {
            return ExistingKinds.TryGetValue(path, out var kind) ? kind : null;
        }

        public PathCheckResult Check(string path)
        {
            CheckCount++;
            return new PathCheckResult(
                path,
                ExistingKinds.ContainsKey(path) ? PathCheckStatus.Available : PathCheckStatus.Missing);
        }
    }
}
