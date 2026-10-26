using System.Collections.Concurrent;

namespace Notes.Api.Notes;

// Used locally when no Cosmos endpoint is configured.
public sealed class InMemoryNotesRepository : INotesRepository
{
    private readonly ConcurrentDictionary<(string UserId, string Id), Note> _notes = new();

    public Task<IReadOnlyList<Note>> ListAsync(string userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Note>>(
            _notes.Values.Where(n => n.UserId == userId).OrderByDescending(n => n.UpdatedAt).ToList());

    public Task<Note?> GetAsync(string userId, string id, CancellationToken ct) =>
        Task.FromResult(_notes.TryGetValue((userId, id), out var note) ? note : null);

    public Task<Note> UpsertAsync(Note note, CancellationToken ct)
    {
        _notes[(note.UserId, note.Id)] = note;
        return Task.FromResult(note);
    }

    public Task<bool> DeleteAsync(string userId, string id, CancellationToken ct) =>
        Task.FromResult(_notes.TryRemove((userId, id), out _));
}
