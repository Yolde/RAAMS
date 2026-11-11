using Microsoft.EntityFrameworkCore;
using RAAMS.Application.Services;
using RAAMS.API.Services;
using RAAMS.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──
builder.Services.AddDbContext<RaamsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ── Services ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ── Controllers ──
builder.Services.AddControllers();

// ── Swagger / OpenAPI ──
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Pipeline ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();