using System.Security.Claims;
using GoGameShop.Frontend.Clients;
using GoGameShop.Frontend.Models;

namespace GoGameShop.Frontend.Services;

public class BasketState(ServerBasketClient basketClient, IHttpContextAccessor httpContextAccessor)
{
    private BasketDto? _cache;
    public event Action? OnChange;

    private Guid UserId =>
        Guid.TryParse(
            httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id
        )
            ? id
            : Guid.Empty;

    public async Task<BasketDto?> GetBasketAsync()
    {
        if (UserId == Guid.Empty)
            return null;
        _cache ??= await basketClient.GetBasketAsync(UserId);
        return _cache;
    }

    public int ItemCount => _cache?.Items.Sum(i => i.Quantity) ?? 0;

    public bool HasGame(Guid gameId) => _cache?.Items.Any(i => i.Id == gameId) ?? false;

    public async Task AddItemAsync(Guid gameId)
    {
        var basket = await GetBasketAsync();
        var items = basket?.Items.ToList() ?? [];
        var existing = items.FirstOrDefault(i => i.Id == gameId);

        if (existing is not null)
            items = items
                .Select(i => i.Id == gameId ? i with { Quantity = i.Quantity + 1 } : i)
                .ToList();
        else
            items.Add(new BasketItemDto(gameId, string.Empty, 0, 1, string.Empty));

        await Sync(items.Select(i => new UpsertBasketItemDto(i.Id, i.Quantity)));
    }

    public async Task UpdateQuantityAsync(Guid gameId, int quantity)
    {
        var basket = await GetBasketAsync();
        var items = (basket?.Items ?? [])
            .Select(i => i.Id == gameId ? i with { Quantity = quantity } : i)
            .Where(i => i.Quantity > 0);
        await Sync(items.Select(i => new UpsertBasketItemDto(i.Id, i.Quantity)));
    }

    public async Task RemoveItemAsync(Guid gameId)
    {
        var basket = await GetBasketAsync();
        var items = (basket?.Items ?? []).Where(i => i.Id != gameId);
        await Sync(items.Select(i => new UpsertBasketItemDto(i.Id, i.Quantity)));
    }

    private async Task Sync(IEnumerable<UpsertBasketItemDto> items)
    {
        await basketClient.UpsertBasketAsync(UserId, items);
        _cache = null;
        OnChange?.Invoke();
    }
}
