namespace GoGameShop.Api.Features.Baskets.GetBasket;

public record BasketDto(Guid CustomerId, IEnumerable<BasketItemDto> Items)
{
    public decimal TotalAmount => Items.Sum(item => item.Price * item.Quantity);
}

public record BasketItemDto(Guid Id, string Name, decimal Price, int Quantity, string ImageUri);
