using System;
using Microsoft.AspNetCore.SignalR;

namespace GoGameShop.Api.Models;

public class Game
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Genre Genre { get; set; } = null!;
    public Rating Rating { get; set; } = null!;
    public DateOnly ReleaseDate { get; set; }
    public decimal Price { get; set; }
    public required string Description { get; set; }

}
