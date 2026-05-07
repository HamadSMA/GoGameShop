namespace GoGameShop.Frontend.Models;

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

public record GameDetailsDto(
    Guid Id,
    string Name,
    Guid GenreId,
    Guid RatingId,
    decimal Price,
    DateOnly ReleaseDate,
    string Description,
    string ImageUri,
    string LastUpdatedBy
);

public class GameFormModel
{
    public string Name { get; set; } = string.Empty;
    public Guid GenreId { get; set; }
    public Guid RatingId { get; set; }
    public decimal Price { get; set; }
    public DateOnly ReleaseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Description { get; set; } = string.Empty;
    public IFormFile? ImageFile { get; set; }
}
