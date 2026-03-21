namespace GoGameShop.Api.Features.Games.GetGames;

public record GameSummaryDto(
    Guid Id,
    string Name,
    string Genre,
    string Rating,
    decimal Price,
    DateOnly ReleaseDate
);
