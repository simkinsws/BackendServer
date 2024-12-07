
using BackendServer.Data;
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
            //using (var scope = app.Services.CreateScope())
            //{
            //    var serviceProvider = scope.ServiceProvider;
            //    var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            //    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            //    // Seed roles (only once)
            //    await RoleSeeder.SeedRolesAsync(serviceProvider, app.Environment);
            //}

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
