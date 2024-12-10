using Microsoft.AspNetCore.Identity;
using System.Text.Json.Serialization;

namespace BackendServer.Data
{
    public class ApplicationUser : IdentityUser
    {
        public List<Post>? Posts { get; set; }
        public string? Name { get; set; }
    }
}
