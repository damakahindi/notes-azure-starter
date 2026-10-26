using System.Security.Claims;

namespace Notes.Api.Notes;

public static class NotesEndpoints
{
    public static IEndpointRouteBuilder MapNotes(this IEndpointRouteBuilder app, bool requireAuth)
    {
        var group = app.MapGroup("/api/notes");
        if (requireAuth)
        {
            group.RequireAuthorization();
        }

        group.MapGet("/", async (ClaimsPrincipal user, INotesRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.ListAsync(UserId(user), ct)));

        group.MapGet("/{id}", async (string id, ClaimsPrincipal user, INotesRepository repo, CancellationToken ct) =>
            await repo.GetAsync(UserId(user), id, ct) is { } note ? Results.Ok(note) : Results.NotFound());

        group.MapPost("/", async (NoteInput input, ClaimsPrincipal user, INotesRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.Title))
            {
                return Results.BadRequest(new { error = "Title is required." });
            }

            var note = new Note(Guid.NewGuid().ToString("N"), UserId(user), input.Title.Trim(), input.Body, DateTimeOffset.UtcNow);
            await repo.UpsertAsync(note, ct);
            return Results.Created($"/api/notes/{note.Id}", note);
        });

        group.MapPut("/{id}", async (string id, NoteInput input, ClaimsPrincipal user, INotesRepository repo, CancellationToken ct) =>
        {
            var userId = UserId(user);
            if (await repo.GetAsync(userId, id, ct) is not { } existing)
            {
                return Results.NotFound();
            }

            var updated = existing with { Title = input.Title.Trim(), Body = input.Body, UpdatedAt = DateTimeOffset.UtcNow };
            return Results.Ok(await repo.UpsertAsync(updated, ct));
        });

        group.MapDelete("/{id}", async (string id, ClaimsPrincipal user, INotesRepository repo, CancellationToken ct) =>
            await repo.DeleteAsync(UserId(user), id, ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }

    // The user id always comes from the validated token, never from the request body or URL.
    private static string UserId(ClaimsPrincipal user) =>
        user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
        ?? user.FindFirstValue("oid")
        ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? "dev-user";
}
