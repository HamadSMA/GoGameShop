using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Data;

public static class DataExtensions
{

    public static void InitializeDb(this WebApplication app)
    {
        app.MigrateDb();
        app.SeedDb();
    }

    private static void MigrateDb(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GoGameShopContext>();
        dbContext.Database.Migrate();
    }

    private static void SeedDb(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GoGameShopContext>();

        if (!dbContext.Genres.Any())
        {
            // When we don't provide an ID, the Databases Provider will generate one for us.
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
        }

        if (!dbContext.Ratings.Any())
        {
            dbContext.Ratings.AddRange(
                new Rating { Name = "Everyone" },
                new Rating { Name = "Teen" },
                new Rating { Name = "Mature" }
            );
        }
        
        dbContext.SaveChanges();
    }
}
