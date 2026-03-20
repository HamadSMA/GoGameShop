namespace GoGameShop.Api.Features.Games.GetGames;

public record GameSummaryDto(
    Guid Id,
    string Name,
    Guid GenreId,
    Guid RatingId,
    decimal Price,
    DateOnly ReleaseDate
);
