namespace GoGameShop.Api.Features.Games.GetGame;

public record GameDetailsDto(
    Guid Id,
    string Name,
    Guid GenreId,
    Guid RatingId,
    decimal Price,
    DateOnly ReleaseDate,
    string Description,
    string ImageUri
);