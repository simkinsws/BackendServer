using Microsoft.AspNetCore.Identity;

namespace BackendServer.Data
{
    public static class RoleSeeder
    {
        public static async Task SeedRolesAndAdminUserAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Define your roles
            var roles = new[] { "Admin", "User" };

            foreach (var role in roles)
            {
                var roleExists = await roleManager.RoleExistsAsync(role);
                if (!roleExists)
                {
                    var result = await roleManager.CreateAsync(new IdentityRole(role));
                    if (result.Succeeded)
                    {
                        Console.WriteLine($"Role '{role}' created.");
                    }
                    else
                    {
                        Console.WriteLine($"Error creating role '{role}'.");
                    }
                }
                else
                {
                    Console.WriteLine($"Role '{role}' already exists.");
                }
            }

            // Create the admin user if not exists
            var adminEmail = "nir.simkin@gmail.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                };

                var createUserResult = await userManager.CreateAsync(user, "Bhrxhnehi12!");

                if (createUserResult.Succeeded)
                {
                    Console.WriteLine("Admin user created.");

                    // Assign the 'Admin' role to the admin user
                    await userManager.AddToRoleAsync(user, "Admin");
                    Console.WriteLine("Admin user assigned to 'Admin' role.");
                }
                else
                {
                    Console.WriteLine("Error creating admin user.");
                }
            }
            else
            {
                Console.WriteLine("Admin user already exists.");
            }
        }
    }
}
