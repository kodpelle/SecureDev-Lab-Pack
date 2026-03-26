using BuggyNotes.Api.Auth;
using BuggyNotes.Api.Data;
using BuggyNotes.Api.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

var insecure = builder.Configuration.GetValue<bool>("Demo:InsecureMode");

var secret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(secret))
{
    secret = "THIS_IS_WEAK_AND_FOR_DEMO_ONLY";
}

var jwtOptions = new JwtOptions
{
    Secret = secret!,
    Issuer = "BuggyNotes",
    Audience = "BuggyNotesAudience",
    ExpiryMinutes = 15
};

builder.Services.AddSingleton(jwtOptions);

builder.Services
  .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(o =>
  {
      o.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuer = true,
          ValidateAudience = true,
          ValidateLifetime = true,
          ValidateIssuerSigningKey = true,
          ValidIssuer = jwtOptions.Issuer,
          ValidAudience = jwtOptions.Audience,
          IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
          ClockSkew = TimeSpan.FromSeconds(30)
      };
  });

builder.Services.AddDbContext<AppDb>(options =>
    options
        .UseSqlite(builder.Configuration.GetConnectionString("Default"))
        .LogTo(Console.WriteLine, LogLevel.Information)
        .EnableSensitiveDataLogging() 
);


builder.Services.AddAuthorization();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.Migrate();
}

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Demo-Mode"] = insecure ? "insecure" : "secure";
    await next();
});


app.UseAuthentication();
app.UseAuthorization();

var log = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("BuggyNotes.App");
log.LogInformation("App started");

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapEndpointsFromAssemblyContaining<AuthEndpoints>();


app.MapGet("/__routes", (IEnumerable<EndpointDataSource> sources) =>
{
    var list = new List<string>();
    foreach (var s in sources)
        foreach (var e in s.Endpoints)
            list.Add(e.DisplayName ?? "(no name)");
    return Results.Ok(list);
});

app.Run();
