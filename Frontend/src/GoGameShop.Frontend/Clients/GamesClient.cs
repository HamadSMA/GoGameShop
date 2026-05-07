using System.Globalization;
using System.Net.Http.Json;
using GoGameShop.Frontend.Models;

namespace GoGameShop.Frontend.Clients;

public class GamesClient(HttpClient http)
{
    public async Task<GamesPageDto> GetGamesAsync(
        int page = 1,
        int pageSize = 5,
        string? name = null
    )
    {
        var url = $"games?pageNumber={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(name))
            url += $"&name={Uri.EscapeDataString(name)}";
        return await http.GetFromJsonAsync<GamesPageDto>(url) ?? new(0, []);
    }

    public async Task<GameDetailsDto?> GetGameAsync(Guid id) =>
        await http.GetFromJsonAsync<GameDetailsDto>($"games/{id}");

    public async Task CreateGameAsync(GameFormModel model)
    {
        var response = await http.PostAsync("games", BuildContent(model));
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateGameAsync(Guid id, GameFormModel model)
    {
        var response = await http.PutAsync($"games/{id}", BuildContent(model));
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteGameAsync(Guid id)
    {
        var response = await http.DeleteAsync($"games/{id}");
        response.EnsureSuccessStatusCode();
    }

    private static MultipartFormDataContent BuildContent(GameFormModel model)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(model.Name), "name" },
            { new StringContent(model.GenreId.ToString()), "genreId" },
            { new StringContent(model.RatingId.ToString()), "ratingId" },
            { new StringContent(model.Price.ToString(CultureInfo.InvariantCulture)), "price" },
            { new StringContent(model.ReleaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "releaseDate" },
            { new StringContent(model.Description), "description" },
        };

        if (model.ImageFile is not null)
        {
            var stream = model.ImageFile.OpenReadStream();
            content.Add(new StreamContent(stream), "imageFile", model.ImageFile.FileName);
        }

        return content;
    }
}
