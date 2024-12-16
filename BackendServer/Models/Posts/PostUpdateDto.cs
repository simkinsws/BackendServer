namespace BackendServer.Models.Posts
{
    public class PostUpdateDto
    {
        public string? Status {  get; set; }
        public string? ImageBase64 { get; set; } // Base64-encoded image data
        public string? ImageMimeType { get; set; } // MIME type (e.g., image/png)
    }
}
