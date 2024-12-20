namespace BackendServer.Models
{
    public class UserResponseDto
    {
        public string? Id { get; set; }
        public string? UserName { get; set; }
        public string? CreatedAt { get; set; }
        public string? LastUpdatedAt { get; set; }
        public string? Email { get; set; }
        public int Posts { get; set; }

    }
}
