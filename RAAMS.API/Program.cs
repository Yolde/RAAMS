using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using RAAMS.API.Authorization;
using RAAMS.API.Filters;
using RAAMS.API.Services;
using RAAMS.Application.Authorization;
using RAAMS.Application.Services;
using RAAMS.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──
builder.Services.AddDbContext<RaamsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ── Services ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IUserProvisioningService, UserProvisioningService>();
builder.Services.AddScoped<IFieldPermissionService, FieldPermissionService>();
builder.Services.AddScoped<IRecordSecurityService, RecordSecurityService>();
builder.Services.AddScoped<IWorkflowAuthorizationService, WorkflowAuthorizationService>();

// ── Authentication ──
// ── Authentication ──
var authMode = builder.Configuration["Authentication:Mode"] ?? "LocalJwt";

if (authMode == "AzureAd")
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}
else
{
    // dotnet user-jwts stores its HMAC signing key in user secrets.
    var signingKeySection = builder.Configuration
        .GetSection("Authentication:Schemes:Bearer:SigningKeys")
        .GetChildren()
        .FirstOrDefault();

    if (signingKeySection == null)
    {
        throw new InvalidOperationException(
            "No JWT signing key found. Run 'dotnet user-jwts key --reset' from the RAAMS.API folder.");
    }

    var signingKeyValue = signingKeySection["Value"]
        ?? throw new InvalidOperationException("JWT signing key has no Value.");

    var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
        Convert.FromBase64String(signingKeyValue));

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                RoleClaimType = "role",
                NameClaimType = "sub",
                ClockSkew = TimeSpan.FromMinutes(5)
            };
        });
}
// ── Authorization Policies ──
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireGRAAdmin", p => p.RequireRole("GRA", "SuperAdmin"));
    options.AddPolicy("RequireLeadAuditor", p => p.RequireRole("LeadAuditor", "GRA", "SuperAdmin"));
    options.AddPolicy("RequireAssuranceCoordinator", p => p.RequireRole("AssuranceCoordinator", "GRA", "SuperAdmin"));
    options.AddPolicy("RequireAuditee", p => p.RequireRole("Auditee", "GRA", "SuperAdmin"));
    options.AddPolicy("RequireActionParty", p => p.RequireRole("ActionParty", "GRA", "SuperAdmin"));
    options.AddPolicy("RequireReviewer", p => p.RequireRole("Reviewer", "GRA", "SuperAdmin"));
    options.AddPolicy("RequireSponsor", p => p.RequireRole("Sponsor", "GRA", "SuperAdmin"));
});
builder.Services.AddSingleton<IAuthorizationHandler, RequireModuleHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, ModulePolicyProvider>();

// ── Controllers ──
builder.Services.AddControllers(options => options.Filters.Add<FieldMaskingFilter>());

// ── Swagger ──
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RAAMS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();