using System.Text.Json.Serialization;

namespace BackendServer.Data
{
    public class Post
    {
        public string? Id { get; set; }
        public string? BranchName { get; set; }
        public string? ContactName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public byte[]? ImageData { get; set; }
        public string? ImageMimeType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UserId { get; set; }
        [JsonIgnore]
        public ApplicationUser? User { get; set; }
    }
}
