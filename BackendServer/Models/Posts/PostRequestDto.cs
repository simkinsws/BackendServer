namespace BackendServer.Models.Posts
{
    public class PostRequestDto
    {
        public string? BranchName { get; set; }
        public string? ContactName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Description { get; set; }
        public string? ImageBase64 { get; set; } // Base64-encoded image data
        public string? ImageMimeType { get; set; } // MIME type (e.g., image/png)
    }
}
