using Castlink.Infrastructure;
using Castlink.Ingestion;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCastlinkInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
