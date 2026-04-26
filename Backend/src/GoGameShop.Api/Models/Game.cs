namespace GoGameShop.Api.Models;

public class Game
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Genre? Genre { get; set; }
    public Guid GenreId { get; set; }
    public Rating? Rating { get; set; }
    public Guid RatingId { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public decimal Price { get; set; }
    public required string Description { get; set; }
    public required string ImageUri { get; set; }
    public required string LastUpdatedBy { get; set; }
}