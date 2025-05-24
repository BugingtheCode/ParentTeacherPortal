using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ParentTeacherAPI.Data;
using ParentTeacherAPI.Hubs;
using ParentTeacherAPI.Models;
using ParentTeacherAPI.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ✅ 1️⃣ Load Configuration Properly


// Ensure configuration is loaded
var configuration = builder.Configuration;
configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);


// ✅ 2️⃣ Add Services to the Container
builder.Services.AddControllers();

// ✅ 3️⃣ Configure Entity Framework Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ 4️⃣ Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ✅ 5️⃣ Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]; // ✅ Matches appsettings.json

if (string.IsNullOrEmpty(jwtKey))
{
    Console.WriteLine("❌ JWT Secret key is missing in configuration.");
    throw new InvalidOperationException("JWT Secret key is missing in configuration.");
}
else
{
    Console.WriteLine($"✅ JWT Secret key loaded: {jwtKey.Substring(0, 5)}****");
}

var key = Encoding.UTF8.GetBytes(jwtKey);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// ✅ 6️⃣ Register Services AFTER Configuration
builder.Services.AddScoped<IAuthService, AuthService>();

// ✅ 7️⃣ Enable CORS
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins("http://localhost:3000") // Change this to your React frontend URL
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials();
                      });
});

// ✅ 8️⃣ Swagger Configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ParentTeacherAPI", Version = "v1" });

    // Explicitly define IFormFile as binary for Swagger
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    c.OperationFilter<FileUploadOperationFilter>();
});
builder.Services.AddSignalR();

// ✅ 9️⃣ Set Web Root
builder.WebHost.UseWebRoot("wwwroot");

var app = builder.Build();

// ✅ 🔟 Configure Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCors(MyAllowSpecificOrigins);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/chatHub");
// ✅ 1️⃣1️⃣ Seed Default Roles & Superuser/Admin User
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // ✅ Define roles
    string[] roles = new[] { "Superuser", "Admin", "Teacher", "Parent" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // ✅ 1️⃣ Seed Superuser (EduNexus Owner)
    string superuserEmail = "owner@edunexus.com";
    string superuserUsername = "EduNexusOwner";
    string superuserPassword = "Super@123"; // Change before production!

    // Check if a Superuser already exists
    var superuserExists = await userManager.Users.AnyAsync(u => u.IsSuperuser);
    if (!superuserExists)
    {
        var superuser = new ApplicationUser
        {
            UserName = superuserUsername,
            Email = superuserEmail,
            IsSuperuser = true
        };

        // ✅ Hash Password Before Storing
        var hashedPassword = userManager.PasswordHasher.HashPassword(superuser, superuserPassword);
        superuser.PasswordHash = hashedPassword;

        var result = await userManager.CreateAsync(superuser);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(superuser, "Superuser");
            Console.WriteLine("✅ Superuser created successfully!");
        }
        else
        {
            Console.WriteLine("❌ Failed to create Superuser. Errors:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($" - {error.Description}");
            }
        }
    }
}


// ✅ 1️⃣2️⃣ Run the Application
app.Run();
