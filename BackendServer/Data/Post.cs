using System.Text.Json.Serialization;

namespace BackendServer.Data
{
    public class Post
    {
        public string? Id { get; set; }
        public string? PostId { get; set; }

        public string? UserId { get; set; }
        [JsonIgnore]
        public ApplicationUser? User { get; set; }
    }
}
