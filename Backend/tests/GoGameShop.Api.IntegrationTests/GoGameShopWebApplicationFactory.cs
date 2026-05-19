using GoGameShop.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GoGameShop.Api.IntegrationTests;

public class GoGameShopWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public GoGameShopWebApplicationFactory()
    {
        // SQLite ":memory:" is per-connection: when the connection closes,
        // the database is gone. Holding one connection open for the
        // lifetime of the factory keeps the in-memory DB alive across
        // every EF Core operation the app performs.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace the production SQLite file with our shared in-memory connection.
            var dbDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<GoGameShopContext>)
            );
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            services.AddDbContext<GoGameShopContext>(options => options.UseSqlite(_connection));

            // Register the test authentication scheme alongside the production ones.
            services
                .AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { }
                );

            // Override Program.cs's default scheme (Keycloak) so every request
            // goes through TestAuthHandler instead. PostConfigure runs last,
            // so this beats whatever the production code set.
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _connection.Dispose();
        base.Dispose(disposing);
    }
}
