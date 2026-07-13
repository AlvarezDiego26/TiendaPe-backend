using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using TiendaPe.Domain.Entities;

namespace TiendaPe.Infrastructure.Persistence;

public partial class TiendaPeDbContext : DbContext
{
    public TiendaPeDbContext(DbContextOptions<TiendaPeDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<CashSession> CashSessions { get; set; }

    public virtual DbSet<Expense> Expenses { get; set; }

    public virtual DbSet<InventoryMovement> InventoryMovements { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Purchase> Purchases { get; set; }

    public virtual DbSet<PurchaseItem> PurchaseItems { get; set; }

    public virtual DbSet<Sale> Sales { get; set; }

    public virtual DbSet<SaleItem> SaleItems { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("inventory_movement_type", new[] { "entry", "sale", "adjustment" })
            .HasPostgresEnum("payment_method", new[] { "cash", "yape_plin" })
            .HasPostgresExtension("extensions", "pgcrypto");

        modelBuilder.Entity<CashSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cash_sessions_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.OpenedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.ClosedBy).WithMany(p => p.ClosedCashSessions).HasConstraintName("cash_sessions_closed_by_fkey");

            entity.HasOne(d => d.OpenedBy).WithMany(p => p.OpenedCashSessions).HasConstraintName("cash_sessions_opened_by_fkey");
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("expenses_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Category);
            entity.Property(e => e.IsRecurring).HasDefaultValue(false);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PaymentMethod).HasColumnType("payment_method");

            entity.HasOne(d => d.CashSession).WithMany(p => p.Expenses).HasConstraintName("expenses_cash_session_id_fkey");
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inventory_movements_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.MovementType).HasColumnType("inventory_movement_type");
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Product).WithMany(p => p.InventoryMovements)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("inventory_movements_product_id_fkey");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("products_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Category).HasDefaultValueSql("'General'::text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MinimumStock).HasDefaultValue(0);
            entity.Property(e => e.Stock).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("purchases_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Supplier).HasDefaultValueSql("'Proveedor'::text");
        });

        modelBuilder.Entity<PurchaseItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("purchase_items_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Product).WithMany(p => p.PurchaseItems)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("purchase_items_product_id_fkey");

            entity.HasOne(d => d.Purchase).WithMany(p => p.PurchaseItems).HasConstraintName("purchase_items_purchase_id_fkey");
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sales_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PaymentMethod).HasColumnType("payment_method");

            entity.HasOne(d => d.CashSession).WithMany(p => p.Sales).HasConstraintName("sales_cash_session_id_fkey");
        });

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sale_items_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Product).WithMany(p => p.SaleItems)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("sale_items_product_id_fkey");

            entity.HasOne(d => d.Sale).WithMany(p => p.SaleItems).HasConstraintName("sale_items_sale_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
