using System.Net.Http.Json;
using GoGameShop.Frontend.Models;

namespace GoGameShop.Frontend.Clients;

public class LookupClient(HttpClient http)
{
    public async Task<IEnumerable<GenreDto>> GetGenresAsync() =>
        await http.GetFromJsonAsync<IEnumerable<GenreDto>>("genres") ?? [];

    public async Task<IEnumerable<RatingDto>> GetRatingsAsync() =>
        await http.GetFromJsonAsync<IEnumerable<RatingDto>>("ratings") ?? [];
}
