using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Data;

public class GoGameShopContext(DbContextOptions<GoGameShopContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<CustomerBasket> Baskets => Set<CustomerBasket>();
    public DbSet<BasketItem> BasketItems => Set<BasketItem>();
}