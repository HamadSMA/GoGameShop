namespace GoGameShop.Api.Features.Games.GetGames;

public record GetGamesDto(int PageNumber = 1, int PageSize = 5, string? Name = null);

public record GamesPageDto(int TotalPages, IEnumerable<GameSummaryDto> Games);

public record GameSummaryDto(
    Guid Id,
    string Name,
    string Genre,
    string Rating,
    decimal Price,
    DateOnly ReleaseDate,
    string ImageUri,
    string LastUpdatedBy
);