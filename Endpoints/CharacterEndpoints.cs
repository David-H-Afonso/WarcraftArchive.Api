using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Services;

namespace WarcraftArchive.Api.Endpoints;

public static class CharacterEndpoints
{
    public static void MapCharacterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/characters").WithTags("Characters").RequireAuthorization();

        group.MapGet("/", async (ICharacterService service, Guid? ownerUserId) =>
            Results.Ok(await service.GetAllAsync(ownerUserId))
        ).WithName("GetCharacters").WithSummary("List all characters, optionally filtered by ownerUserId");

        group.MapGet("/{id:guid}", async (Guid id, ICharacterService service) =>
        {
            var character = await service.GetByIdAsync(id);
            return character == null ? Results.NotFound() : Results.Ok(character);
        }).WithName("GetCharacterById").WithSummary("Get a character by ID");

        group.MapPost("/", async (CreateCharacterRequest request, ICharacterService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required." });
            if (string.IsNullOrWhiteSpace(request.Class))
                return Results.BadRequest(new { message = "Class is required." });

            var character = await service.CreateAsync(request);
            return Results.Created($"/characters/{character.Id}", character);
        }).WithName("CreateCharacter").WithSummary("Create a new character");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCharacterRequest request, ICharacterService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required." });
            if (string.IsNullOrWhiteSpace(request.Class))
                return Results.BadRequest(new { message = "Class is required." });

            var character = await service.UpdateAsync(id, request);
            return character == null ? Results.NotFound() : Results.Ok(character);
        }).WithName("UpdateCharacter").WithSummary("Update a character");

        group.MapDelete("/{id:guid}", async (Guid id, ICharacterService service) =>
        {
            var deleted = await service.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteCharacter").WithSummary("Delete a character");
    }
}
