using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Data;

public static class DataExtensions
{
    public static async Task InitializeDbAsync(this WebApplication app)
    {
        if (Environment.GetEnvironmentVariable("ASPNETCORE_APIDESCRIPTION_GENERATE") is null)
        {
            app.Logger.LogInformation("Skipping database initialization (OpenAPI generation mode)");
            return;
        }

        await app.MigrateDbAsync();
        await app.SeedDbAsync();
        app.Logger.LogInformation("Database initialized");
    }

    private static async Task MigrateDbAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GoGameShopContext>();
        await dbContext.Database.MigrateAsync();
    }

    private static async Task SeedDbAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GoGameShopContext>();

        if (!dbContext.Genres.Any())
            // When we don't provide an ID, the Database Provider will generate one for us.
            dbContext.Genres.AddRange(
                new Genre { Name = "Action" },
                new Genre { Name = "Adventure" },
                new Genre { Name = "RPG" },
                new Genre { Name = "Strategy" },
                new Genre { Name = "Simulation" },
                new Genre { Name = "Sports" },
                new Genre { Name = "Puzzle" },
                new Genre { Name = "Racing" },
                new Genre { Name = "Fighting" }
            );

        if (!dbContext.Ratings.Any())
            dbContext.Ratings.AddRange(
                new Rating { Name = "Everyone" },
                new Rating { Name = "Teen" },
                new Rating { Name = "Mature" }
            );

        await dbContext.SaveChangesAsync();
    }
}
