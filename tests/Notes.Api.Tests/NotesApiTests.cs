using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Notes.Api.Notes;

namespace Notes.Api.Tests;

public class NotesApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_returns_ok()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_then_list_returns_the_note()
    {
        var created = await _client.PostAsJsonAsync("/api/notes", new NoteInput("First", "hello"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var notes = await _client.GetFromJsonAsync<List<Note>>("/api/notes");
        Assert.Contains(notes!, n => n.Title == "First");
    }

    [Fact]
    public async Task Create_without_title_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/notes", new NoteInput(" ", "body"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_the_note()
    {
        var created = await (await _client.PostAsJsonAsync("/api/notes", new NoteInput("Temp", "x"))).Content.ReadFromJsonAsync<Note>();
        var deleted = await _client.DeleteAsync($"/api/notes/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/notes/{created.Id}")).StatusCode);
    }
}
