using System.Net;
using Microsoft.Azure.Cosmos;

namespace Notes.Api.Notes;

public sealed class CosmosNotesRepository(Container container) : INotesRepository
{
    public async Task<IReadOnlyList<Note>> ListAsync(string userId, CancellationToken ct)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId ORDER BY c.updatedAt DESC")
            .WithParameter("@userId", userId);
        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(userId) };

        var results = new List<Note>();
        using var iterator = container.GetItemQueryIterator<Note>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            results.AddRange(await iterator.ReadNextAsync(ct));
        }
        return results;
    }

    public async Task<Note?> GetAsync(string userId, string id, CancellationToken ct)
    {
        try
        {
            var response = await container.ReadItemAsync<Note>(id, new PartitionKey(userId), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Note> UpsertAsync(Note note, CancellationToken ct)
    {
        var response = await container.UpsertItemAsync(note, new PartitionKey(note.UserId), cancellationToken: ct);
        return response.Resource;
    }

    public async Task<bool> DeleteAsync(string userId, string id, CancellationToken ct)
    {
        try
        {
            await container.DeleteItemAsync<Note>(id, new PartitionKey(userId), cancellationToken: ct);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
