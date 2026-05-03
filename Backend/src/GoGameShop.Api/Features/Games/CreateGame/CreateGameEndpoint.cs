using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GoGameShop.Api.Shared.Authorization;
using GoGameShop.Api.Shared.FileUpload;
using Microsoft.AspNetCore.Mvc;

namespace GoGameShop.Api.Features.Games.CreateGame;

public static class CreateGameEndpoint
{
    private const string DefaultImageUri = "https://placehold.co/150";

    public static void MapCreateGame(this IEndpointRouteBuilder app)
    {
        // POST /games
        app.MapPost(
                "/",
                async (
                    GoGameShopContext dbContext,
                    [FromForm] CreateGameDto gameDto,
                    ILogger<Program> logger,
                    FileUploader fileUploader,
                    ClaimsPrincipal user
                ) =>
                {
                    if (user?.Identity?.IsAuthenticated == false)
                    {
                        return Results.Unauthorized();
                    }

                    var currentUserId = user?.FindFirstValue(JwtRegisteredClaimNames.Sub);

                    if (String.IsNullOrEmpty(currentUserId))
                    {
                        return Results.Unauthorized();
                    }

                    var imageUri = DefaultImageUri;

                    if (gameDto.ImageFile is not null)
                    {
                        var fileUploadResult = await fileUploader.UploadFileAsync(
                            gameDto.ImageFile,
                            StorageNames.GameImagesFolder
                        );

                        if (!fileUploadResult.IsSuccess)
                            return Results.BadRequest(
                                new { message = fileUploadResult.ErrorMessage }
                            );
                        imageUri = fileUploadResult.FileUrl;
                    }

                    Game game =
                        new()
                        {
                            Name = gameDto.Name,
                            GenreId = gameDto.GenreId,
                            RatingId = gameDto.RatingId,
                            ReleaseDate = gameDto.ReleaseDate,
                            Price = gameDto.Price,
                            Description = gameDto.Description,
                            ImageUri = imageUri!,
                            LastUpdatedBy = currentUserId
                        };

                    dbContext.Add(game);
                    await dbContext.SaveChangesAsync();

                    logger.LogInformation(
                        "Created Game {GameName} with price {GamePrice}",
                        game.Name,
                        game.Price
                    );

                    return Results.CreatedAtRoute(
                        EndpointNames.GetGame,
                        new { id = game.Id },
                        new GameDetailsDto(
                            game.Id,
                            game.Name,
                            game.GenreId,
                            game.RatingId,
                            game.ReleaseDate,
                            game.Price,
                            game.Description,
                            game.ImageUri,
                            game.LastUpdatedBy
                        )
                    );
                }
            )
            .WithParameterValidation()
            .DisableAntiforgery()
            .RequireAuthorization(Policies.AdminAccess);
    }
}