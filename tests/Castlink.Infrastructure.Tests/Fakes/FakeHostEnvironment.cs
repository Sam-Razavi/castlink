using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Castlink.Infrastructure.Tests.Fakes;

/// <summary>Minimal <see cref="IHostEnvironment"/> double — avoids pulling in a hosting test-host
/// package just to flip <see cref="EnvironmentName"/> for a warning-log assertion.</summary>
internal sealed class FakeHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;

    public string ApplicationName { get; set; } = "Castlink.Infrastructure.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
