namespace Notes.Api.Notes;

// Every call is scoped by userId: it is also the Cosmos partition key.
public interface INotesRepository
{
    Task<IReadOnlyList<Note>> ListAsync(string userId, CancellationToken ct);
    Task<Note?> GetAsync(string userId, string id, CancellationToken ct);
    Task<Note> UpsertAsync(Note note, CancellationToken ct);
    Task<bool> DeleteAsync(string userId, string id, CancellationToken ct);
}
