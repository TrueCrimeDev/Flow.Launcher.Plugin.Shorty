namespace Shorty.Models;

public sealed class AnswerEntry
{
    public required string Id { get; init; }

    public required string Question { get; init; }

    public required string Text { get; init; }

    public required string Preset { get; init; }

    public required string Model { get; init; }

    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;

    public bool IsError { get; init; }

    public string ErrorTitle { get; init; } = "";
}
