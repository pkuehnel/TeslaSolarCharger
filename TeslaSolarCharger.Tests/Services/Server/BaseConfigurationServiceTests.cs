using System.IO;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class BaseConfigurationServiceTests : TestBase
{
    public BaseConfigurationServiceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task DownloadAutoBackup_GivenPathTraversalFileName_ShouldThrowOrBePrevented()
    {
        // Arrange
        using var mock = Mock.Create<BaseConfigurationService>();
        var fileName = "../../../appsettings.json";
        var baseDir = Path.Combine(Path.GetTempPath(), "BaseConfigurationServiceTests");
        Directory.CreateDirectory(baseDir);

        mock.Mock<IConfigurationWrapper>()
            .Setup(x => x.AutoBackupsZipDirectory())
            .Returns(baseDir);

        var service = mock.Create<BaseConfigurationService>();

        // Let's create a file outside the baseDir to simulate the target of traversal
        var secretFile = Path.Combine(baseDir, "../secret.txt");
        await File.WriteAllTextAsync(secretFile, "secret content");

        // Act & Assert
        // After fix, this should fail to find the file because it looks for "secret.txt" inside baseDir, not ../secret.txt
        await Assert.ThrowsAsync<FileNotFoundException>(async () => await service.DownloadAutoBackup("../secret.txt"));

        // Verify that valid file works
        var validFile = Path.Combine(baseDir, "valid.zip");
        await File.WriteAllTextAsync(validFile, "valid content");
        var result = await service.DownloadAutoBackup("valid.zip");
        Assert.Equal("valid content", System.Text.Encoding.UTF8.GetString(result));

        // Cleanup
        if(File.Exists(secretFile)) File.Delete(secretFile);
        if(Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
    }
}
