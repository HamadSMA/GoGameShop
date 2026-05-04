using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GoGameShop.Api.Shared.Authorization;
using GoGameShop.Api.Shared.FileUpload;
using Microsoft.AspNetCore.Mvc;

namespace GoGameShop.Api.Features.Games.UpdateGame;

public static class UpdateGameEndpoint
{
    public static void MapUpdateGame(this IEndpointRouteBuilder app)
    {
        // PUT /games/{id}
        app.MapPut(
                "/{id}",
                async (
                    Guid id,
                    GoGameShopContext DbContext,
                    [FromForm] UpdateGameDto gameDto,
                    FileUploader fileUploader,
                    ClaimsPrincipal user
                ) =>
                {
                    var currentUserId =
                        user?.FindFirstValue(JwtRegisteredClaimNames.Email)
                        ?? user?.FindFirstValue(JwtRegisteredClaimNames.Sub);

                    if (String.IsNullOrEmpty(currentUserId))
                    {
                        return Results.Unauthorized();
                    }

                    var existingGame = await DbContext.Games.FindAsync(id);

                    if (existingGame is null)
                        return Results.NotFound("Game not found");

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

                        existingGame.ImageUri = fileUploadResult.FileUrl!;
                    }

                    existingGame.Name = gameDto.Name;
                    existingGame.GenreId = gameDto.GenreId;
                    existingGame.RatingId = gameDto.RatingId;
                    existingGame.ReleaseDate = gameDto.ReleaseDate;
                    existingGame.Price = gameDto.Price;
                    existingGame.Description = gameDto.Description;
                    existingGame.LastUpdatedBy = currentUserId;

                    await DbContext.SaveChangesAsync();

                    return Results.NoContent();
                }
            )
            .WithParameterValidation()
            .DisableAntiforgery()
            .RequireAuthorization(Policies.AdminAccess);
    }
}
