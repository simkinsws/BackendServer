
using BackendServer.Data;
using BackendServer.Models.Posts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BackendServer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
             var builder = WebApplication.CreateBuilder(args);

            var connectionString = builder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Connection string 'ApplicationDbContextConnection' not found.");

            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

            builder.Services.AddAuthorization();


            builder.Services.AddIdentityApiEndpoints<ApplicationUser>().AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Apply migrations at startup (optional if you're using migrations)
            using (var scope = app.Services.CreateScope())
            {
                var serviceProvider = scope.ServiceProvider;
                var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

                // Seed roles and admin user (only once)
                await RoleSeeder.SeedRolesAndAdminUserAsync(serviceProvider);
            }



            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.MapIdentityApi<ApplicationUser>();

            app.MapPost("/api/posts/create", async (PostRequestDto postDto, HttpContext httpContext, ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager) =>
            {
                // Get the logged-in user's ID
                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    return Results.Unauthorized();
                }

                // Retrieve the user from the database (optional for validation)
                var user = await userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Results.NotFound("User not found.");
                }

                // Convert Base64 string to byte array
                byte[]? imageData = null;
                if (!string.IsNullOrEmpty(postDto.ImageBase64))
                {
                    try
                    {
                        imageData = Convert.FromBase64String(postDto.ImageBase64);
                    }
                    catch (FormatException)
                    {
                        return Results.BadRequest("Invalid image format.");
                    }
                }

                // Create a new post and assign it to the user
                var newPost = new Post
                {
                    Id = Guid.NewGuid().ToString(),
                    BranchName = postDto.BranchName,
                    ContactName = postDto.ContactName,
                    PhoneNumber = postDto.PhoneNumber,
                    Description = postDto.Description,
                    ImageData = imageData,
                    UserId = userId
                };

                dbContext.Posts.Add(newPost);
                await dbContext.SaveChangesAsync();

                return Results.Ok(newPost);
            }).RequireAuthorization();




            app.MapPut("/update-phone-number", async (UserManager<ApplicationUser> userManager, ClaimsPrincipal user, string newPhoneNumber) =>
            {
                var currentUser = await userManager.GetUserAsync(user);
                if (currentUser == null)
                {
                    return Results.NotFound("User not found.");
                }

                currentUser.PhoneNumber = newPhoneNumber;

                var result = await userManager.UpdateAsync(currentUser);

                if (result.Succeeded)
                {
                    return Results.Ok("Phone number updated successfully.");
                }
                else
                {
                    return Results.BadRequest("Failed to update phone number.");
                }
            }).RequireAuthorization();


            app.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
            {

                await signInManager.SignOutAsync();
                return Results.Ok();

            }).RequireAuthorization();


            app.MapGet("/pingauth", (ClaimsPrincipal user) =>
            {
                var email = user.FindFirstValue(ClaimTypes.Email); // get the user's email from the claim
                return Results.Json(new { Email = email }); ; // return the email as a plain text response
            }).RequireAuthorization();

            app.MapGet("/api/posts/mine", async (HttpContext httpContext, ApplicationDbContext dbContext) =>
            {
                // Get the logged-in user's ID from the claims
                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    return Results.Unauthorized();
                }

                // Fetch the user's posts from the database
                var userPosts = await dbContext.Posts
                    .Where(p => p.UserId == userId)
                    .ToListAsync();

                return Results.Ok(userPosts);
            })
            .RequireAuthorization(); // Ensure the user is authenticated


            app.MapGet("/api/posts/user/{identifier}", async (string identifier, HttpContext httpContext, ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager) =>
            {
                // Check if the logged-in user is an Admin
                if (!httpContext.User.IsInRole("Admin"))
                {
                    return Results.Forbid();
                }

                // Determine if the identifier is a UserName or UserId
                ApplicationUser user;
                if (Guid.TryParse(identifier, out _)) // If it's a GUID, assume it's a UserId
                {
                    user = await userManager.FindByIdAsync(identifier);
                }
                else // Otherwise, assume it's a UserName
                {
                    user = await userManager.FindByNameAsync(identifier);
                }

                if (user == null)
                {
                    return Results.NotFound("User not found.");
                }

                // Fetch the posts for the specified user
                var userPosts = await dbContext.Posts
                    .Where(p => p.UserId == user.Id)
                    .ToListAsync();

                return Results.Ok(userPosts);
            })
            .RequireAuthorization("AdminPolicy"); // Only Admins can access this endpoint


            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
