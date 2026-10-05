using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ShiftHandOver.Server.Models;

public partial class ShiftHandoverDbContext : DbContext
{
    public ShiftHandoverDbContext()
    {
    }

    public ShiftHandoverDbContext(DbContextOptions<ShiftHandoverDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<BranchBank> BranchBanks { get; set; }

    public virtual DbSet<PosConfig> PosConfigs { get; set; }

    public virtual DbSet<Shift> Shifts { get; set; }

    public virtual DbSet<ShiftBankEntry> ShiftBankEntries { get; set; }

    public virtual DbSet<ShiftEmployee> ShiftEmployees { get; set; }

    public virtual DbSet<ShiftExpense> ShiftExpenses { get; set; }

    public virtual DbSet<ShiftPosEntry> ShiftPosEntries { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(config.GetConnectionString("DBContext"));
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Vietnamese_CI_AS");

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Branches__3214EC07B8617790");

            entity.Property(e => e.DefaultCashOpening)
                .HasDefaultValue(2000000m)
                .HasColumnType("decimal(18, 0)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<BranchBank>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BranchBa__3214EC07E63599B7");

            entity.HasIndex(e => new { e.BranchId, e.SlotIndex }, "UQ_BranchBanks_BranchSlot").IsUnique();

            entity.Property(e => e.BankName).HasMaxLength(100);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchBanks)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BranchBanks_Branches");
        });

        modelBuilder.Entity<PosConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PosConfi__3214EC074C611EBD");

            entity.Property(e => e.DisplayOrder).HasDefaultValue((byte)1);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PosName).HasMaxLength(100);

            entity.HasOne(d => d.Branch).WithMany(p => p.PosConfigs)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PosConfigs_Branches");
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Shifts__3214EC07E0F4D9CD");

            entity.HasIndex(e => new { e.BranchId, e.ShiftDate }, "IX_Shifts_BranchDate").IsDescending(false, true);

            entity.HasIndex(e => e.Status, "IX_Shifts_Status");

            entity.HasIndex(e => new { e.BranchId, e.ShiftDate, e.ShiftType }, "UQ_Shifts_BranchDateType").IsUnique();

            entity.Property(e => e.CashClosing).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.CashDifference).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.CashOpening)
                .HasDefaultValue(2000000m)
                .HasColumnType("decimal(18, 0)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.ActiveChannels).HasMaxLength(100);
            entity.Property(e => e.ShiftType).HasMaxLength(15);
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .HasDefaultValue("OPEN");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Branch).WithMany(p => p.Shifts)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Shifts_Branches");

            entity.HasOne(d => d.ClosedByUser).WithMany(p => p.ShiftClosedByUsers)
                .HasForeignKey(d => d.ClosedByUserId)
                .HasConstraintName("FK_Shifts_ClosedBy");

            entity.HasOne(d => d.OpenedByUser).WithMany(p => p.ShiftOpenedByUsers)
                .HasForeignKey(d => d.OpenedByUserId)
                .HasConstraintName("FK_Shifts_OpenedBy");
        });

        modelBuilder.Entity<ShiftBankEntry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ShiftBan__3214EC0771BFD6C0");

            entity.HasIndex(e => e.ShiftId, "IX_ShiftBankEntries_ShiftId");

            entity.HasIndex(e => new { e.ShiftId, e.BranchBankId }, "UQ_ShiftBankEntries_ShiftBank").IsUnique();

            entity.Property(e => e.BankClosing).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.BankClosingDay1).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.BankClosingDay2).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.BankOpening).HasColumnType("decimal(18, 0)");

            entity.HasOne(d => d.BranchBank).WithMany(p => p.ShiftBankEntries)
                .HasForeignKey(d => d.BranchBankId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftBankEntries_BranchBanks");

            entity.HasOne(d => d.Shift).WithMany(p => p.ShiftBankEntries)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftBankEntries_Shifts");
        });

        modelBuilder.Entity<ShiftEmployee>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_ShiftEmployees_Max2"));

            entity.HasKey(e => e.Id).HasName("PK__ShiftEmp__3214EC0730E9A7FD");

            entity.HasIndex(e => e.UserId, "IX_ShiftEmployees_UserId");

            entity.HasIndex(e => new { e.ShiftId, e.UserId }, "UQ_ShiftEmployees_ShiftUser").IsUnique();

            entity.HasOne(d => d.Shift).WithMany(p => p.ShiftEmployees)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftEmployees_Shifts");

            entity.HasOne(d => d.User).WithMany(p => p.ShiftEmployees)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftEmployees_Users");
        });

        modelBuilder.Entity<ShiftExpense>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ShiftExp__3214EC077E867DB9");

            entity.HasIndex(e => e.ShiftId, "IX_ShiftExpenses_ShiftId");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.ShiftExpenses)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftExpenses_Users");

            entity.HasOne(d => d.Shift).WithMany(p => p.ShiftExpenses)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftExpenses_Shifts");
        });

        modelBuilder.Entity<ShiftPosEntry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ShiftPos__3214EC07BD15D7E8");

            entity.HasIndex(e => e.ShiftId, "IX_ShiftPosEntries_ShiftId");

            entity.HasIndex(e => new { e.ShiftId, e.PosConfigId }, "UQ_ShiftPosEntries_ShiftPos").IsUnique();

            entity.Property(e => e.PosClosing).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.PosClosingDay1).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.PosClosingDay2).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.PosOpening).HasColumnType("decimal(18, 0)");

            entity.HasOne(d => d.PosConfig).WithMany(p => p.ShiftPosEntries)
                .HasForeignKey(d => d.PosConfigId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftPosEntries_PosConfigs");

            entity.HasOne(d => d.Shift).WithMany(p => p.ShiftPosEntries)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftPosEntries_Shifts");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC076BA187A6");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E41B0F93FB").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
