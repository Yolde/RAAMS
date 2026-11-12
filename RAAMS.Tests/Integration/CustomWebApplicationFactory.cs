using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests.Integration;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new(
        $"Data Source=raams_test_{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Foreign Keys=True");

    public TestData Data { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Authentication:Mode"] = "LocalJwt",
                ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] =
                    Convert.ToBase64String(new byte[32]),
                ["Jwt:Issuer"] = "raams-integration-tests"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<RaamsDbContext>();
            services.RemoveAll<DbContextOptions<RaamsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RaamsDbContext>>();
            services.AddDbContext<RaamsDbContext, SqliteTestDbContext>(options => options.UseSqlite(_connection));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _connection.Open();
        IHost? host = null;
        try
        {
            host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RaamsDbContext>();
            db.Database.EnsureCreated();
            Data = TestSeeder.SeedAsync(db).GetAwaiter().GetResult();
            return host;
        }
        catch
        {
            host?.Dispose();
            _connection.Dispose();
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
