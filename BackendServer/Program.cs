
using BackendServer.Data;
using BackendServer.Models.Posts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
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

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins("http://localhost:3000")
                          .WithOrigins("https://proud-forest-04617c61e.4.azurestaticapps.net")  // Frontend URL
                          .AllowAnyMethod()                   // Allow any HTTP method (GET, POST, etc.)
                          .AllowAnyHeader()                   // Allow any headers
                          .AllowCredentials();                // Allow credentials (cookies, authorization headers)
                });
            });

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
            app.UseCors("AllowFrontend");

            app.MapIdentityApi<ApplicationUser>();

            app.MapPost("/reset-password", async (
                    [FromBody] ResetPasswordRequest request,
                    UserManager<ApplicationUser> userManager
                ) =>
            {
                var user = await userManager.FindByNameAsync(request.Email)
                           ?? await userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return Results.NotFound(new { Message = "User not found." });
                }

                // Check for password validity (optional, ensures password meets policy)
                var passwordValidationResult = await userManager.PasswordValidators[0].ValidateAsync(userManager, user, request.NewPassword);
                if (!passwordValidationResult.Succeeded)
                {
                    return Results.ValidationProblem(passwordValidationResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
                }

                // Reset the password directly
                var resetResult = await userManager.RemovePasswordAsync(user);
                if (!resetResult.Succeeded)
                {
                    return Results.ValidationProblem(resetResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
                }

                var addPasswordResult = await userManager.AddPasswordAsync(user, request.NewPassword);
                if (!addPasswordResult.Succeeded)
                {
                    return Results.ValidationProblem(addPasswordResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
                }

                return Results.Ok(new { Message = "Password reset successful." });
            });

            app.MapPost("/api/admin/registerUser", async Task<IResult> ([FromBody] RegisterRequest registration, ClaimsPrincipal userClaims, [FromServices] UserManager<ApplicationUser> userManager, [FromServices] RoleManager<IdentityRole> roleManager) =>
            {
                var userRole = userClaims.Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value)
                        .ToList().FirstOrDefault();
                if (string.IsNullOrEmpty(userRole) || !userRole.Equals("Admin"))
                {
                    return Results.Forbid();
                }    
                // Email validation
                if (string.IsNullOrEmpty(registration.Email) || !new EmailAddressAttribute().IsValid(registration.Email))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        { "Email", new[] { "Invalid email format." } }
                    });
                }

                // Create a new user
                var user = new ApplicationUser { UserName = registration.Email, Email = registration.Email };
                var result = await userManager.CreateAsync(user, registration.Password);

                if (!result.Succeeded)
                {
                    return Results.ValidationProblem(result.Errors
                        .ToDictionary(e => e.Code, e => new[] { e.Description }));
                }

                // Assign default "User" role if it doesn't exist
                var defaultRole = "User";
                var roleExists = await roleManager.RoleExistsAsync(defaultRole);
                if (!roleExists)
                {
                    // Create the "User" role if it doesn't exist
                    await roleManager.CreateAsync(new IdentityRole(defaultRole));
                }

                // Assign the "User" role to the newly created user
                await userManager.AddToRoleAsync(user, defaultRole);

                // Return success response without email confirmation
                return Results.Ok(new { Message = "Registration successful." });
            }).RequireAuthorization().WithTags("Admin Endpoints");

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
                    ImageMimeType = postDto.ImageMimeType,
                    UserId = userId,
                    Status = "Open"
                };

                dbContext.Posts.Add(newPost);
                await dbContext.SaveChangesAsync();

                return Results.Ok(newPost);
            }).RequireAuthorization().WithTags("Posts Endpoints");

            app.MapPut("/api/post/update", async (PostUpdateDto postUpdateDto, ApplicationDbContext dbContext, HttpContext httpContext) => {
                if(!httpContext.User.IsInRole("Admin"))
                {
                    return Results.Forbid();
                }

                var post = await dbContext.Posts.SingleOrDefaultAsync(post => post.Id == postUpdateDto.Id);
                if (post != null) 
                {
                    byte[]? imageData = null;
                    if (!string.IsNullOrEmpty(postUpdateDto.ImageBase64))
                    {
                        try
                        {
                            imageData = Convert.FromBase64String(postUpdateDto.ImageBase64);
                        }
                        catch (FormatException)
                        {
                            return Results.BadRequest("Invalid image format.");
                        }
                    }
                    post.Status = postUpdateDto!.Status!.Equals(post.Status) ? post.Status : postUpdateDto.Status ;
                    post.Description = postUpdateDto!.Description!.Equals(post.Description) ? post.Description : postUpdateDto.Description;
                    if(imageData != null)
                    {
                        post.ImageData = imageData!.Equals(post.ImageData) ? post.ImageData : imageData;
                        post.ImageMimeType = postUpdateDto!.ImageMimeType!.Equals(post.ImageMimeType) ? post.ImageMimeType : postUpdateDto.ImageMimeType;
                    }
                }
                await dbContext.SaveChangesAsync();
                return Results.Ok(post);
            }).RequireAuthorization().WithTags("Posts Endpoints");

            app.MapPut("/api/users/update-phone-number", async (UserManager<ApplicationUser> userManager, ClaimsPrincipal user, string newPhoneNumber) =>
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
            }).RequireAuthorization().WithTags("Users Endpoints");


            app.MapPost("api/users/logout", async (SignInManager<ApplicationUser> signInManager) =>
            {

                await signInManager.SignOutAsync();
                return Results.Ok();

            }).RequireAuthorization().WithTags("Users Endpoints");


            app.MapGet("/api/users/pingauth", (ClaimsPrincipal user) =>
            {
                var email = user.FindFirstValue(ClaimTypes.Email); // get the user's email from the claim
                return Results.Json(new { Email = email }); ; // return the email as a plain text response
            }).RequireAuthorization().WithTags("User Claims");

            app.MapGet("/api/users/userRole", async (ClaimsPrincipal user, ApplicationDbContext dbContext) =>
            {
                // Get the logged-in user's ID from the claims
                var userRole = user.Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value)
                        .ToList().FirstOrDefault();
                if (string.IsNullOrEmpty(userRole))
                {
                    return Results.NotFound();
                }

                return Results.Ok(userRole);
            }).WithTags("User Claims");

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
            .RequireAuthorization().WithTags("Posts Endpoints"); // Ensure the user is authenticated

            app.MapGet("/api/posts/all", async (HttpContext httpContext, ApplicationDbContext dbContext) =>
            {
                // Check if the logged-in user is an Admin
                if (!httpContext.User.IsInRole("Admin"))
                {
                    return Results.Forbid();
                }

                var allPosts = await dbContext.Posts.ToListAsync();

                return Results.Ok(allPosts);
            })
            .RequireAuthorization().WithTags("Admin Endpoints"); // Only Admins can access this endpoint

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
            .RequireAuthorization().WithTags("Posts Endpoints"); // Only Admins can access this endpoint


            // Configure the HTTP request pipeline.
            // if (app.Environment.IsDevelopment()) // better for real application to devide by environments.
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                options.RoutePrefix = "swagger";
            });

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
