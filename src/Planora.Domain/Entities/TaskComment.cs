namespace Planora.Domain.Entities;

public class TaskComment
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }

    public string AuthorUserId { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public TaskItem TaskItem { get; set; } = null!;
}