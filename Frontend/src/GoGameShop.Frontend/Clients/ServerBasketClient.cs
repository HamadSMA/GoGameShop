using System.Net.Http.Json;
using GoGameShop.Frontend.Models;

namespace GoGameShop.Frontend.Clients;

public class ServerBasketClient(HttpClient http)
{
    public async Task<BasketDto?> GetBasketAsync(Guid customerId) =>
        await http.GetFromJsonAsync<BasketDto>($"baskets/{customerId}");

    public async Task UpsertBasketAsync(Guid customerId, IEnumerable<UpsertBasketItemDto> items)
    {
        var response = await http.PutAsJsonAsync(
            $"baskets/{customerId}",
            new UpsertBasketDto(items)
        );
        response.EnsureSuccessStatusCode();
    }
}
