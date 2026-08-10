using Castlink.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCastlinkInfrastructure(builder.Configuration);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
