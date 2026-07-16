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
dataSourceBuilder.MapEnum<InventoryMovementReason>("public.inventory_movement_reason");
dataSourceBuilder.MapEnum<PaymentMethod>("public.payment_method");
dataSourceBuilder.MapEnum<ProductTrackingType>("public.product_tracking_type");
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
        do $$
        begin
            create type product_tracking_type as enum ('unit', 'package', 'weight', 'bulk');
        exception
            when duplicate_object then null;
        end $$;

        do $$
        begin
            create type inventory_movement_reason as enum ('purchase', 'sale', 'adjustment', 'return', 'initial_stock', 'waste');
        exception
            when duplicate_object then null;
        end $$;

        do $$
        begin
            create type payment_method as enum ('cash', 'yape_plin', 'yape', 'plin', 'transfer');
        exception
            when duplicate_object then null;
        end $$;

        alter type payment_method add value if not exists 'yape';
        alter type payment_method add value if not exists 'plin';
        alter type payment_method add value if not exists 'transfer';

        alter table expenses
            add column if not exists recurring_start date;

        alter table expenses
            add column if not exists recurring_end date;

        create table if not exists suppliers (
            id uuid primary key default gen_random_uuid(),
            name text not null,
            phone text null,
            company text null,
            notes text null,
            last_purchase_at timestamptz null,
            total_purchased numeric(12,2) not null default 0,
            pending_balance numeric(12,2) not null default 0,
            is_active boolean not null default true,
            created_at timestamptz not null default now(),
            updated_at timestamptz not null default now()
        );

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
            add column if not exists notes text,
            add column if not exists base_unit text not null default 'unidad',
            add column if not exists tracking_type product_tracking_type not null default 'unit',
            add column if not exists profit_margin_percent numeric(8,2),
            add column if not exists suggested_price numeric(12,2),
            add column if not exists purchase_unit text,
            add column if not exists sale_unit text,
            add column if not exists units_per_package numeric(12,3) not null default 1,
            add column if not exists entry_date date,
            add column if not exists supplier_id uuid references suppliers(id),
            add column if not exists stock_base numeric(14,3) not null default 0,
            add column if not exists minimum_stock_base numeric(14,3) not null default 0,
            add column if not exists average_cost_base numeric(12,4) not null default 0;

        create table if not exists product_presentations (
            id uuid primary key default gen_random_uuid(),
            product_id uuid not null references products(id) on delete cascade,
            name text not null,
            unit_label text not null,
            quantity_in_base_unit numeric(14,3) not null default 1,
            purchase_enabled boolean not null default true,
            sale_enabled boolean not null default true,
            barcode text null,
            purchase_cost numeric(12,2) null,
            sale_price numeric(12,2) null,
            wholesale_price numeric(12,2) null,
            suggested_price numeric(12,2) null,
            profit_margin_percent numeric(8,2) null,
            is_default_purchase boolean not null default false,
            is_default_sale boolean not null default false,
            is_active boolean not null default true,
            created_at timestamptz not null default now(),
            updated_at timestamptz not null default now()
        );

        create table if not exists inventory_movement_logs (
            id uuid primary key default gen_random_uuid(),
            product_id uuid not null references products(id),
            presentation_id uuid null references product_presentations(id),
            reason inventory_movement_reason not null,
            quantity_input numeric(14,3) not null,
            input_unit text not null,
            quantity_base numeric(14,3) not null,
            unit_cost_base numeric(12,4) null,
            total_cost numeric(12,2) null,
            reference_table text null,
            reference_id uuid null,
            notes text null,
            occurred_at timestamptz not null default now(),
            created_at timestamptz not null default now()
        );

        alter table purchases
            add column if not exists supplier_id uuid references suppliers(id),
            add column if not exists payment_method text not null default 'cash',
            add column if not exists paid_amount numeric(12,2) not null default 0,
            add column if not exists pending_amount numeric(12,2) not null default 0,
            add column if not exists notes text;

        alter table purchase_items
            add column if not exists presentation_id uuid references product_presentations(id),
            add column if not exists quantity_base numeric(14,3) not null default 0,
            add column if not exists input_unit text,
            add column if not exists units_per_package numeric(14,3) not null default 1,
            add column if not exists total_cost numeric(12,2),
            add column if not exists unit_cost_base numeric(12,4),
            add column if not exists suggested_price numeric(12,2),
            add column if not exists profit_margin_percent numeric(8,2);

        alter table sale_items
            add column if not exists presentation_id uuid references product_presentations(id),
            add column if not exists quantity_base numeric(14,3) not null default 0,
            add column if not exists input_unit text,
            add column if not exists unit_cost_base numeric(12,4),
            add column if not exists profit numeric(12,2);

        alter table cash_sessions
            add column if not exists cash_sales numeric(12,2) not null default 0,
            add column if not exists yape_sales numeric(12,2) not null default 0,
            add column if not exists plin_sales numeric(12,2) not null default 0,
            add column if not exists transfer_sales numeric(12,2) not null default 0,
            add column if not exists supplier_payments numeric(12,2) not null default 0,
            add column if not exists personal_withdrawals numeric(12,2) not null default 0,
            add column if not exists final_cash numeric(12,2);

        alter table expenses
            add column if not exists supplier_id uuid references suppliers(id),
            add column if not exists is_supplier_payment boolean not null default false;

        create table if not exists cash_movements (
            id uuid primary key default gen_random_uuid(),
            cash_session_id uuid not null references cash_sessions(id),
            type text not null,
            payment_method text not null default 'cash',
            amount numeric(12,2) not null,
            description text null,
            reference_table text null,
            reference_id uuid null,
            occurred_at timestamptz not null default now(),
            created_at timestamptz not null default now()
        );

        create index if not exists idx_products_active_id
            on products (is_active, id);

        create index if not exists idx_products_active_name
            on products (is_active, name);

        create index if not exists idx_products_active_barcode
            on products (is_active, barcode);

        create index if not exists idx_product_presentations_product
            on product_presentations (product_id);

        create index if not exists idx_product_presentations_barcode
            on product_presentations (barcode)
            where barcode is not null;

        create index if not exists idx_inventory_movement_logs_product
            on inventory_movement_logs (product_id, occurred_at desc);

        create index if not exists idx_suppliers_active
            on suppliers (is_active, name);

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
