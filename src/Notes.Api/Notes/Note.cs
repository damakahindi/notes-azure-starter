namespace Notes.Api.Notes;

public sealed record Note(
    string Id,
    string UserId,
    string Title,
    string Body,
    DateTimeOffset UpdatedAt);

public sealed record NoteInput(string Title, string Body);
