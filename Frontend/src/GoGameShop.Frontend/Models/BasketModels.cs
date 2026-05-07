namespace GoGameShop.Frontend.Models;

public record BasketDto(Guid CustomerId, IEnumerable<BasketItemDto> Items);

public record BasketItemDto(Guid Id, string Name, decimal Price, int Quantity, string ImageUri);

public record UpsertBasketDto(IEnumerable<UpsertBasketItemDto> Items);

public record UpsertBasketItemDto(Guid GameId, int Quantity);
