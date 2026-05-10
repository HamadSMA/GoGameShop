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
                        "In a reimagined late 16th-century Sengoku Japan, you play as Wolf, a shinobi sworn to protect his kidnapped lord and restore his severed arm. Guided by a mysterious prosthetic hand with interchangeable tools, you fight through crumbling castles, haunted forests, and ancient temples. Sekiro's posture-based combat demands precision and aggression in equal measure — every encounter is a lethal exchange of deflections, counterattacks, and hard-earned death-defying resurrection.",
                    Price = 49.99m,
                    ReleaseDate = new DateOnly(2019, 3, 22),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/814380/library_600x900.jpg",
                    Genre = action,
                    Rating = mature,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Death Stranding",
                    Description =
                        "America lies fractured after a cataclysmic event blurred the boundary between the living and the dead. You are Sam Porter Bridges, a courier tasked with reconnecting isolated cities across a vast and haunting wasteland. Every delivery is a trek through treacherous terrain, invisible ghost-like enemies, and timefall rain that ages everything it touches. Forge connections, carry hope, and rebuild a broken nation one strand at a time in Hideo Kojima's boldest vision.",
                    Price = 39.99m,
                    ReleaseDate = new DateOnly(2019, 11, 8),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/1190460/library_600x900.jpg",
                    Genre = adventure,
                    Rating = mature,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Uncharted 4: A Thief's End",
                    Description =
                        "Years after retiring from treasure hunting, Nathan Drake is pulled back in when his long-lost brother Sam resurfaces with a dangerous secret. Together they race across the globe — from Madagascan jungles to crumbling Scottish strongholds — in pursuit of Captain Avery's legendary pirate colony. Uncharted 4 delivers cinematic action, sharp dialogue, and breathtaking set-pieces in a heartfelt and thrilling send-off for one of gaming's most beloved adventurers.",
                    Price = 19.99m,
                    ReleaseDate = new DateOnly(2016, 5, 10),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/1659420/library_600x900.jpg",
                    Genre = adventure,
                    Rating = teen,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Elden Ring",
                    Description =
                        "Step into the Lands Between, a shattered realm whose Elden Ring has been broken, its fragments scattered among demigod rulers consumed by ambition and madness. As a Tarnished warrior guided by a faint grace, you explore a vast open world of interconnected dungeons, crumbling fortresses, and poison swamps. Co-created with George R.R. Martin, Elden Ring blends FromSoftware's punishing combat with sweeping mythological storytelling unlike anything before it.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2022, 2, 25),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/1245620/library_600x900.jpg",
                    Genre = rpg,
                    Rating = mature,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "The Witcher 3: Wild Hunt",
                    Description =
                        "As Geralt of Rivia, the world's most feared monster hunter, you track the Wild Hunt — a spectral army hunting your adopted daughter Ciri across a war-ravaged continent. The Northern Kingdoms burn while you navigate betrayal, broken alliances, and creatures from myth. Every contract is a moral puzzle; every choice reshapes the world. With two massive DLC expansions included, The Witcher 3 remains the gold standard for open-world RPGs.",
                    Price = 39.99m,
                    ReleaseDate = new DateOnly(2015, 5, 19),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/292030/library_600x900.jpg",
                    Genre = rpg,
                    Rating = mature,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Persona 5 Royal",
                    Description =
                        "By day, Joker is a quiet transfer student on probation. By night, he leads the Phantom Thieves — a crew of teenagers who invade the subconscious Metaverse to reform corrupt adults by stealing the twisted desires festering in their hearts. Stylish turn-based combat, a gorgeous jazz-soaked soundtrack, and deep social simulation weave into a story about rebellion, identity, and justice. Persona 5 Royal is one of the most distinct JRPGs ever made.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2020, 3, 31),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/1687950/library_600x900.jpg",
                    Genre = rpg,
                    Rating = teen,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Civilization VI",
                    Description =
                        "Lead one of dozens of historical civilizations from the stone age to the space age. In Civilization VI, cities physically expand across the map — each district built on the land itself, giving territory real strategic weight. Research technologies and civics on parallel trees, send envoys to city-states, declare war, forge alliances, or race to launch a satellite. Every playthrough tells a different story of how humanity might have risen to rule the world.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2016, 10, 21),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/289070/library_600x900.jpg",
                    Genre = strategy,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Stardew Valley",
                    Description =
                        "Burned out and handed your grandfather's neglected farm, you leave the corporate grind behind for Pelican Town — a sleepy valley full of secrets and eccentric locals. Plant crops, raise animals, mine deep caverns for ore, and fish the rivers to restore the community center. Beneath the cozy surface lies surprising depth: branching relationships, a mysterious underworld, and a world that quietly asks what it means to build a life worth living.",
                    Price = 14.99m,
                    ReleaseDate = new DateOnly(2016, 2, 26),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/413150/library_600x900.jpg",
                    Genre = simulation,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "FIFA 24",
                    Description =
                        "FIFA 24 captures the world's game with HyperMotionV technology — motion data from hundreds of real matches feeding lifelike animations across every position. Updated squads, licensed stadiums, and refined set-piece mechanics make the beautiful game feel closer than ever. Ultimate Team returns with deeper squad-building options, and Volta Football keeps street soccer alive. Whether you play local or online seasons, the pitch is always yours.",
                    Price = 69.99m,
                    ReleaseDate = new DateOnly(2023, 9, 29),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/2195250/library_600x900.jpg",
                    Genre = sports,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Portal 2",
                    Description =
                        "Aperture Science has flooded its test chambers with a passive-aggressive AI and your only tool is a gun that punches holes in space. Portal 2 escalates the original's elegant puzzles into an epic story of corporate rivalry, robot rebellion, and hard-won escape. The co-op campaign doubles the challenge with two portal guns in play simultaneously. Few games are this clever, this funny, and this endlessly replayable — it's a masterpiece of design.",
                    Price = 9.99m,
                    ReleaseDate = new DateOnly(2011, 4, 19),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/620/library_600x900.jpg",
                    Genre = puzzle,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Forza Horizon 5",
                    Description =
                        "Set across an enormous open-world recreation of Mexico — from active volcanoes to ancient ruins to sun-soaked beaches — Forza Horizon 5 is a love letter to the art of driving. Choose from over 500 cars ranging from vintage classics to modern hypercars, each modeled with obsessive authenticity. Dynamic seasons shift weather, terrain, and events every week. Whether you're racing rivals or simply cruising at sunset, it never stops looking breathtaking.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2021, 11, 9),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/1551360/library_600x900.jpg",
                    Genre = racing,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Street Fighter 6",
                    Description =
                        "Street Fighter 6 reimagines the series with the Drive System — a unified mechanic powering parries, overdrives, and rushdowns from a single resource bar. World Tour drops you into a living Metro City to train under legendary fighters and build a custom character. Battle Hub connects players worldwide in a persistent arcade space. With stunning animation and the deepest roster in series history, it's the most complete Street Fighter ever made.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2023, 6, 2),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/1794960/library_600x900.jpg",
                    Genre = fighting,
                    Rating = teen,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "DOOM Eternal",
                    Description =
                        "Hell has invaded Earth and the Doom Slayer is the only thing standing between humanity and extinction. DOOM Eternal transforms the shooter into a relentless resource puzzle — ammunition runs dry, so you chainsaw demons for bullets; armor chips away, so you flamethrow for shielding; health depletes, so you glory-kill for pickups. Every second is a high-speed juggling act against lethally designed demons. It's the most demanding, exhilarating FPS ever made.",
                    Price = 39.99m,
                    ReleaseDate = new DateOnly(2020, 3, 20),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/782330/library_600x900.jpg",
                    Genre = action,
                    Rating = mature,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Red Dead Redemption 2",
                    Description =
                        "As the Van der Linde gang's most capable outlaw, Arthur Morgan rides across the dying American frontier in 1899 — a world that no longer has room for men like him. Pinkerton agents close in and the gang fractures from within as Arthur wrestles with loyalty and his own fading sense of honor. Hunt, fish, rob trains, or simply watch a thunderstorm roll across the plains. Rockstar's most ambitious world, built for those willing to truly live in it.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2018, 10, 26),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/1174180/library_600x900.jpg",
                    Genre = adventure,
                    Rating = mature,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Hollow Knight",
                    Description =
                        "Descend into Hallownest — a vast, decaying underground kingdom where ancient bugs once built a civilization that fell to a mysterious plague. As a silent nameless knight, you explore interconnected caverns armed with a nail and growing mastery of soul-powered spells. Hollow Knight is a hand-drawn metroidvania with no map markers and no hand-holding — only the joy of discovery and the sting of a difficult boss remembered long after the credits roll.",
                    Price = 14.99m,
                    ReleaseDate = new DateOnly(2017, 2, 24),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/367520/library_600x900.jpg",
                    Genre = action,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Dark Souls III",
                    Description =
                        "The fire is fading for the last time, and the Lords of Cinder have abandoned their thrones. As the Ashen One, you are summoned to rekindle the First Flame — but the path is lined with grotesque undead, towering knights, and bosses of legendary design. Dark Souls III distills everything FromSoftware learned across a generation into its most refined, most cinematic entry. Fast, lethal, and hauntingly beautiful — the series at its absolute peak.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2016, 3, 24),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/374320/library_600x900.jpg",
                    Genre = rpg,
                    Rating = mature,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Age of Empires IV",
                    Description =
                        "Age of Empires IV brings one of PC gaming's most beloved strategy series into the modern era with eight distinct civilizations, each playing fundamentally differently. Four documentary-style campaigns span the Norman Conquest, the Mongol Empire, the Hundred Years' War, and the rise of Moscow. Every age advance unlocks new units, buildings, and options. Whether you're a returning veteran or new to the genre, it rewards every style of play.",
                    Price = 59.99m,
                    ReleaseDate = new DateOnly(2021, 10, 28),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/1466860/library_600x900.jpg",
                    Genre = strategy,
                    Rating = teen,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Cities: Skylines",
                    Description =
                        "Cities: Skylines hands you a blank plot of land and the tools to shape it into a metropolis. Zone residential, commercial, and industrial districts, then watch citizens move in, commute, and complain. Traffic jams, pollution, and budget shortfalls are constant adversaries. Specialized districts let you build tourism hubs, university campuses, and industrial corridors. With deep simulation under the hood and a massive modding community, no two cities ever grow alike.",
                    Price = 29.99m,
                    ReleaseDate = new DateOnly(2015, 3, 10),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/255710/library_600x900.jpg",
                    Genre = simulation,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "NBA 2K24",
                    Description =
                        "NBA 2K24 sets a new benchmark for basketball simulation with ProPLAY — a system that translates real NBA broadcast footage directly into in-game animations for unprecedented realism. Kobe Bryant is the cover star and MyCareer lets you forge a legacy on his level from the G-League upward. The City returns as a massive open online hub. Reworked shot-timing and defensive rotations make the on-court feel the most authentic the series has ever delivered.",
                    Price = 69.99m,
                    ReleaseDate = new DateOnly(2023, 9, 8),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/2338770/library_600x900.jpg",
                    Genre = sports,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                },
                new Game
                {
                    Name = "Tetris Effect: Connected",
                    Description =
                        "Tetris Effect takes the world's most timeless puzzle game and transforms it into a transcendent sensory experience. Every line cleared sends ripples of light, sound, and music pulsing across stunning environments — from coral reefs to aurora-lit tundras to the surface of the moon. The Zone mechanic lets you freeze time at critical moments to clear a crisis stack. Connected adds three-player co-op and versus modes. It's Tetris as you've never felt it before.",
                    Price = 39.99m,
                    ReleaseDate = new DateOnly(2020, 11, 10),
                    ImageUri = "https://cdn.cloudflare.steamstatic.com/steam/apps/1147940/library_600x900.jpg",
                    Genre = puzzle,
                    Rating = everyone,
                    LastUpdatedBy = "seed"
                }
            );

            await dbContext.SaveChangesAsync();
        }
    }
}
