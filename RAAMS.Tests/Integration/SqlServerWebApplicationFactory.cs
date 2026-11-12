using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests.Integration;

internal sealed class SqlServerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private bool _created;
    public TestData Data { get; private set; } = null!;

    public SqlServerWebApplicationFactory()
    {
        var source = Environment.GetEnvironmentVariable("RAAMS_TEST_SQL_CONNECTION")
            ?? throw new InvalidOperationException("Set RAAMS_TEST_SQL_CONNECTION to a SQL Server test instance.");
        _connectionString = new SqlConnectionStringBuilder(source)
        {
            InitialCatalog = $"RaamsHttpTest_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Mode"] = "LocalJwt",
            ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = Convert.ToBase64String(new byte[32]),
            ["Jwt:Issuer"] = "raams-integration-tests"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<RaamsDbContext>();
            services.RemoveAll<DbContextOptions<RaamsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RaamsDbContext>>();
            services.AddDbContext<RaamsDbContext>(options => options.UseSqlServer(_connectionString));
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
        var host = base.CreateHost(builder);
        try
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RaamsDbContext>();
            db.GetService<IRelationalDatabaseCreator>().Create();
            _created = true;
            db.Database.Migrate();
            Data = TestSeeder.SeedAsync(db).GetAwaiter().GetResult();
            return host;
        }
        catch
        {
            if (_created)
            {
                using var scope = host.Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<RaamsDbContext>().Database.EnsureDeleted();
                _created = false;
            }
            host.Dispose();
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _created)
        {
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<RaamsDbContext>().Database.EnsureDeleted();
            _created = false;
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_created)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<RaamsDbContext>().Database.EnsureDeletedAsync();
            _created = false;
        }
        await base.DisposeAsync();
    }
}
