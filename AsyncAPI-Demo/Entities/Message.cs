using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AsyncAPI_Demo.Entities;

public class Message
{
    [BsonElement("id")]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonElement("groupId")]
    [BsonRepresentation(BsonType.String)]
    public Guid GroupId { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;

    public string Sentiment { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; } = null;
}

