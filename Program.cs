using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TiendaPe.Domain.Entities;
using TiendaPe.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TiendaPeDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = builder.Configuration["TIENDAPE_DB"];
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'TiendaPeDb' or environment variable 'TIENDAPE_DB' is not configured.");
}

connectionString = NormalizePostgresConnectionString(connectionString);

var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString)
{
    NoResetOnClose = true
};
connectionString = connectionBuilder.ConnectionString;

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<InventoryMovementType>("public.inventory_movement_type");
dataSourceBuilder.MapEnum<PaymentMethod>("public.payment_method");
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<TiendaPeDbContext>(options =>
    options.UseNpgsql(dataSource, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(60);
    }));

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    jwtSecret = "tiendape-development-secret-change-before-production-2026";
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TiendaPe",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TiendaPe",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (configuredOrigins.Length > 0)
        {
            policy.WithOrigins(configuredOrigins).AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await EnsureDatabasePerformanceAsync(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (TiendaPeDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    return Results.Ok(new { status = canConnect ? "ok" : "database_unreachable", database = canConnect });
})
.AllowAnonymous();

app.MapControllers();

app.Run();

static async Task EnsureDatabasePerformanceAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TiendaPeDbContext>();
    db.Database.SetCommandTimeout(120);

    await db.Database.ExecuteSqlRawAsync("""
        alter table expenses
            add column if not exists recurring_start date;

        alter table expenses
            add column if not exists recurring_end date;

        alter table products
            add column if not exists internal_code text,
            add column if not exists barcode text,
            add column if not exists brand text,
            add column if not exists presentation text,
            add column if not exists unit text,
            add column if not exists supplier text,
            add column if not exists wholesale_price numeric(12,2),
            add column if not exists expiration_date date,
            add column if not exists location text,
            add column if not exists notes text;

        create index if not exists idx_products_active_id
            on products (is_active, id);

        create index if not exists idx_products_active_name
            on products (is_active, name);

        create index if not exists idx_products_active_barcode
            on products (is_active, barcode);

        create index if not exists idx_cash_sessions_open
            on cash_sessions (closed_at, opened_at desc);

        create index if not exists idx_sales_cash_session_method
            on sales (cash_session_id, payment_method);

        create index if not exists idx_sales_occurred_at
            on sales (occurred_at);

        create index if not exists idx_expenses_cash_session_method
            on expenses (cash_session_id, payment_method);

        create index if not exists idx_expenses_occurred_at
            on expenses (occurred_at);

        create index if not exists idx_sale_items_sale_id
            on sale_items (sale_id);

        create index if not exists idx_sale_items_product_id
            on sale_items (product_id);
        """);
}

static string NormalizePostgresConnectionString(string connectionString)
{
    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
        (uri.Scheme != "postgresql" && uri.Scheme != "postgres"))
    {
        return connectionString;
    }

    var userInfo = uri.UserInfo.Split(':', 2);
    var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    var database = uri.AbsolutePath.TrimStart('/');

    return new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = Uri.UnescapeDataString(database),
        Username = username,
        Password = password,
        SslMode = SslMode.Require
    }.ConnectionString;
}
