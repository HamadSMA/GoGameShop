namespace GoGameShop.Api.Features.Games.CreateGame;

public record CreateGameDto(
    [Required] [StringLength(50)] string Name,
    Guid GenreId,
    Guid RatingId,
    DateOnly ReleaseDate,
    [Range(1, 100)] decimal Price,
    [Required] [StringLength(500)] string Description
);

public record GameDetailsDto(
    Guid Id,
    string Name,
    Guid GenreId,
    Guid RatingId,
    DateOnly ReleaseDate,
    decimal Price,
    string Description
);
