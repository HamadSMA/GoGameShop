using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Data;

public static class DataExtensions
{
    public static async Task InitializeDbAsync(this WebApplication app)
    {
        if (Environment.GetEnvironmentVariable("ASPNETCORE_APIDESCRIPTION_GENERATE") is not null)
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

        if (!dbContext.Games.Any())
        {
            var action = dbContext.Genres.First(g => g.Name == "Action");
            var adventure = dbContext.Genres.First(g => g.Name == "Adventure");
            var rpg = dbContext.Genres.First(g => g.Name == "RPG");
            var strategy = dbContext.Genres.First(g => g.Name == "Strategy");
            var simulation = dbContext.Genres.First(g => g.Name == "Simulation");
            var sports = dbContext.Genres.First(g => g.Name == "Sports");
            var puzzle = dbContext.Genres.First(g => g.Name == "Puzzle");
            var racing = dbContext.Genres.First(g => g.Name == "Racing");
            var fighting = dbContext.Genres.First(g => g.Name == "Fighting");

            var everyone = dbContext.Ratings.First(r => r.Name == "Everyone");
            var teen = dbContext.Ratings.First(r => r.Name == "Teen");
            var mature = dbContext.Ratings.First(r => r.Name == "Mature");

            dbContext.Games.AddRange(
                new Game
                {
                    Name = "Sekiro: Shadows Die Twice",
                    Description =
                        "A shinobi's quest for revenge in a dark, fantastical version of late 1500s Japan.",
                    Price = 49.99m,
                    ReleaseDate = new DateOnly(2019, 3, 22),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=Sekiro",
                    Genre = action,
                    Rating = mature,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "The Legend of Zelda: Breath of the Wild",
                    Description =
                        "Explore a vast open-world Hyrule, solve shrines, and defeat Calamity Ganon.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2017, 3, 3),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=Zelda+BotW",
                    Genre = adventure,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Uncharted 4: A Thief's End",
                    Description =
                        "Nathan Drake embarks on one final globe-trotting adventure in search of a legendary pirate treasure.",
                    Price = 19.99m,
                    ReleaseDate = new DateOnly(2016, 5, 10),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=Uncharted+4",
                    Genre = adventure,
                    Rating = teen,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Elden Ring",
                    Description =
                        "An open-world FromSoftware RPG set in the Lands Between, co-created with George R.R. Martin.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2022, 2, 25),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=Elden+Ring",
                    Genre = rpg,
                    Rating = mature,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "The Witcher 3: Wild Hunt",
                    Description =
                        "Geralt of Rivia hunts a powerful enemy across a massive open world rich with choice-driven quests.",
                    Price = 39.99m,
                    ReleaseDate = new DateOnly(2015, 5, 19),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=Witcher+3",
                    Genre = rpg,
                    Rating = mature,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Persona 5 Royal",
                    Description =
                        "High school students become Phantom Thieves, stealing corrupted desires from the hearts of adults.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2020, 3, 31),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=Persona+5+Royal",
                    Genre = rpg,
                    Rating = teen,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Civilization VI",
                    Description =
                        "Build an empire to stand the test of time — research technology, wage war, and forge diplomacy.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2016, 10, 21),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=Civilization+VI",
                    Genre = strategy,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Stardew Valley",
                    Description =
                        "Inherit your grandfather's farm and build a new life in the charming Pelican Town.",
                    Price = 14.99m,
                    ReleaseDate = new DateOnly(2016, 2, 26),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=Stardew+Valley",
                    Genre = simulation,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "FIFA 24",
                    Description =
                        "The world's most popular football simulation with updated rosters and HyperMotionV technology.",
                    Price = 69.99m,
                    ReleaseDate = new DateOnly(2023, 9, 29),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=FIFA+24",
                    Genre = sports,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Portal 2",
                    Description =
                        "Use a portal gun to solve mind-bending physics puzzles in Aperture Science's deadly test chambers.",
                    Price = 9.99m,
                    ReleaseDate = new DateOnly(2011, 4, 19),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=Portal+2",
                    Genre = puzzle,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Forza Horizon 5",
                    Description =
                        "Race through a stunning open-world Mexico in hundreds of cars across dynamic seasons.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2021, 11, 9),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=Forza+Horizon+5",
                    Genre = racing,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Street Fighter 6",
                    Description =
                        "A new era of street fighting with the Drive System, World Tour mode, and a massive roster.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2023, 6, 2),
                    ImageUri = "https://placehold.co/300x400/1a1a2e/ffffff?text=Street+Fighter+6",
                    Genre = fighting,
                    Rating = teen,
                    LastUpdatedBy = "seed"
                }
            );

            await dbContext.SaveChangesAsync();
        }
    }
}
