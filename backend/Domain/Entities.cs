namespace Chatbot.Api.Domain;

public enum MessageRole
{
    User,
    Assistant,
    System
}

public enum ModalityKind
{
    Text,
    Image,
    Video,
    Audio,
    Speech,
    Document,
    Embedding
}

/// <summary>A chat session groups related messages for a single user.</summary>
public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = default!;
    public string Title { get; set; } = "New chat";
    public string? ModelId { get; set; }
    public bool AutomaticModel { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ChatMessage> Messages { get; set; } = new();
}

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public ChatSession? Session { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ModelIdUsed { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<StoredFile> Attachments { get; set; } = new();
}

/// <summary>Metadata for any input/output artifact stored in S3.</summary>
public class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = default!;
    public Guid? MessageId { get; set; }
    public ChatMessage? Message { get; set; }
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string S3Key { get; set; } = default!;
    public ModalityKind Kind { get; set; }
    public bool IsOutput { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
