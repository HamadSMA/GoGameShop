namespace GoGameShop.Api.Features.Baskets.UpsertBasket;

public record UpsertBasketDto(IEnumerable<UpsertBasketItemDto> Items);

public record UpsertBasketItemDto(Guid GameId, int Quantity);