using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Planora.Infrastructure.Logging;

namespace Planora.IntegrationTests;

public sealed class SystemLogPagingTests
{
    [Fact]
    public async Task LogPagingReturnsNewestFirstAndKeepsOnlyTheRequestedPage()
    {
        string root = Path.Combine(Path.GetTempPath(), "planora-log-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        try
        {
            await File.WriteAllLinesAsync(Path.Combine(root, "logs", "planora-20261001.log"),
            [
                "2026-10-01 08:00:00.000 +00:00 [INF] first",
                "2026-10-01 09:00:00.000 +00:00 [WRN] second",
                "2026-10-01 10:00:00.000 +00:00 [ERR] third",
                "2026-10-01 11:00:00.000 +00:00 [INF] fourth",
                "2026-10-01 12:00:00.000 +00:00 [WRN] fifth"
            ]);
            var environment = new TestHostEnvironment { ContentRootPath = root };
            var service = new LogReaderService(environment);

            var first = await service.GetLogsPageAsync(new DateTime(2026, 10, 1),
                new DateTime(2026, 10, 1), "All", page: 1, pageSize: 2);
            var second = await service.GetLogsPageAsync(new DateTime(2026, 10, 1),
                new DateTime(2026, 10, 1), "All", page: 2, pageSize: 2);
            var warnings = await service.GetLogsPageAsync(new DateTime(2026, 10, 1),
                new DateTime(2026, 10, 1), "Warning", page: 1, pageSize: 10);

            Assert.Equal(5, first.TotalCount);
            Assert.Equal(new[] { "fifth", "fourth" }, first.Items.Select(x => x.Message));
            Assert.Equal(new[] { "third", "second" }, second.Items.Select(x => x.Message));
            Assert.Equal(2, warnings.TotalCount);
            Assert.All(warnings.Items, item => Assert.Equal("Warning", item.Level));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Planora.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
