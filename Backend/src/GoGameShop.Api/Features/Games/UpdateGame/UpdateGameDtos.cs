namespace GoGameShop.Api.Features.Games.UpdateGame;

public record UpdateGameDto(
    [Required][StringLength(50)] string Name,
    Guid GenreId,
    Guid RatingId,
    DateOnly ReleaseDate,
    [Range(1, 100)] decimal Price,
    [Required][StringLength(500)] string Description
);
