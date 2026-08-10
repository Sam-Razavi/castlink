using Castlink.Application.Graph;
using Castlink.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCastlinkInfrastructure(builder.Configuration);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Load the actor/film graph into memory once, at startup (see docs/PLAN.md Phase 2) — blocks
// startup until it's ready rather than serving requests against an empty graph. At ~1M credits
// this is a one-time cost of maybe tens of seconds, not something worth backgrounding for v1.
using (var startupScope = app.Services.CreateScope())
{
    var graphSource = startupScope.ServiceProvider.GetRequiredService<IGraphSnapshotSource>();
    var graphSnapshotProvider = startupScope.ServiceProvider.GetRequiredService<GraphSnapshotProvider>();
    var snapshot = await InMemoryGraphSnapshot.BuildAsync(graphSource.StreamEdgesAsync(CancellationToken.None));
    graphSnapshotProvider.SetSnapshot(snapshot);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Minimal observable surface for Phase 0 — confirms the host starts and DI
// resolves correctly before Phase 1 adds the database and real endpoints.
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Exposed for WebApplicationFactory<Program> in Castlink.Api.Tests.
public partial class Program;
