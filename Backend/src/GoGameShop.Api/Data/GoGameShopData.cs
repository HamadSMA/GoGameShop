// NO longer used, kept for reference.




// namespace GoGameShop.Api.Data;
//
// public class GoGameShopData
// {
//
//     readonly List<Genre> genres = [
//         new () {
//             Id = new Guid("935B4AE6-F3A3-442E-B984-32DC6EF7DAF9"),
//             Name = "Stealth Action"
//         },
//         new () {
//             Id = new Guid("F0E7D90C-5EE0-4E93-9DFA-647DE26ED59E"),
//             Name = "Action RPG"
//         },
//         new () {
//             Id = new Guid ("C3FDB962-4C68-4D30-B4E3-F2AF5C46B4C5"),
//             Name = "Platformer"
//         }
//     ];
//
//     readonly List<Rating> ratings = [
//         new () {
//             Id = new Guid("31ED9D0E-21C0-48AD-A305-2159BDD5E241"),
//             Name = "Mature"
//         },
//         new () {
//             Id = new Guid("A15FB383-11FA-4658-8E52-E591D545E46E"),
//             Name = "Teen"
//         },
//         new () {
//             Id = new Guid("553F39A5-7CB5-416B-B268-E6D1D9AAFFB2"),
//             Name = "Everyone"
//         }
//     ];
//
//     private readonly List<Game> games;
//     public GoGameShopData()
//     {
//
//         games = [
//             new () {
//             Id = Guid.NewGuid(),
//             Name = "Metal Gear Solid",
//             Genre = genres[0],
//             GenreId = genres[0].Id,
//             Rating = ratings[0],
//             RatingId = ratings[0].Id,
//             ReleaseDate = new DateOnly(1998, 9, 3),
//             Price = 19.99m,
//             Description = "A tactical espionage action game following Solid Snake as he infiltrates a nuclear weapons facility."
//         },
//         new () {
//             Id = Guid.NewGuid(),
//             Name = "Monster Hunter: World",
//             Genre = genres[1],
//             GenreId = genres[1].Id,
//             Rating = ratings[1],
//             RatingId = ratings[1].Id,
//             ReleaseDate = new DateOnly(2018, 1, 26),
//             Price = 29.99m,
//             Description = "Hunt massive monsters in a dynamic ecosystem, crafting weapons and armor from your fallen prey."
//         },
//         new () {
//             Id = Guid.NewGuid(),
//             Name = "Super Mario Galaxy",
//             Genre = genres[2],
//             GenreId = genres[2].Id,
//             Rating = ratings[2],
//             RatingId = ratings[2].Id,
//             ReleaseDate = new DateOnly(2007, 11, 1),
//             Price = 24.99m,
//             Description = "Join Mario on a cosmic adventure across gravity-defying planets to rescue Princess Peach."
//         }
//         ];
//     }
//
//     public IEnumerable<Game> GetGames => games;
//     public IEnumerable<Genre> GetGenres => genres;
//     public IEnumerable<Rating> GetRatings => ratings;
//     public Game? GetGame(Guid id) => games.Find(game => game.Id == id);
//     public Genre? GetGenre(Guid id) => genres.Find(genre => genre.Id == id);
//     public Rating? GetRating(Guid id) => ratings.Find(rating => rating.Id == id);
//     public void AddGame(Game game)
//     {
//         game.Id = Guid.NewGuid();
//         games.Add(game);
//     }
//     public void RemoveGame(Guid id) => games.RemoveAll(game => game.Id == id);
// }
