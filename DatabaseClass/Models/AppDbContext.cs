using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DatabaseClass.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Campaign> Campaigns { get; set; }

    public virtual DbSet<CampaignCompletion> CampaignCompletions { get; set; }

    public virtual DbSet<CampaignDocument> CampaignDocuments { get; set; }

    public virtual DbSet<CampaignImage> CampaignImages { get; set; }

    public virtual DbSet<CompletionImage> CompletionImages { get; set; }

    public virtual DbSet<Donation> Donations { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=DELL;Database=CharityPlatform;User Id=sa;Password=root;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Campaign__3214EC0792861D59");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.GoalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.User).WithMany(p => p.Campaigns)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Campaigns_Users");
        });

        modelBuilder.Entity<CampaignCompletion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Campaign__3214EC07E1031C6D");

            entity.HasIndex(e => e.CampaignId, "UQ__Campaign__3F5E8A980CFE473B").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Campaign).WithOne(p => p.CampaignCompletion)
                .HasForeignKey<CampaignCompletion>(d => d.CampaignId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CampaignCompletions_Campaigns");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CampaignCompletions)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CampaignCompletions_Admin");
        });

        modelBuilder.Entity<CampaignDocument>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Campaign__3214EC0706CB48E2");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.DocumentType).HasMaxLength(100);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasOne(d => d.Campaign).WithMany(p => p.CampaignDocuments)
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CampaignDocuments_Campaigns");
        });

        modelBuilder.Entity<CampaignImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Campaign__3214EC0751202C1A");

            entity.Property(e => e.Caption).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasOne(d => d.Campaign).WithMany(p => p.CampaignImages)
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CampaignImages_Campaigns");
        });

        modelBuilder.Entity<CompletionImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Completi__3214EC0710397AE1");

            entity.Property(e => e.Caption).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasOne(d => d.Completion).WithMany(p => p.CompletionImages)
                .HasForeignKey(d => d.CompletionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CompletionImages_Completions");
        });

        modelBuilder.Entity<Donation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Donation__3214EC0729C29B91");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.TransferScreenshot).HasMaxLength(500);

            entity.HasOne(d => d.Campaign).WithMany(p => p.Donations)
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Donations_Campaigns");

            entity.HasOne(d => d.Donor).WithMany(p => p.DonationDonors)
                .HasForeignKey(d => d.DonorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Donations_Donor");

            entity.HasOne(d => d.VerifiedByNavigation).WithMany(p => p.DonationVerifiedByNavigations)
                .HasForeignKey(d => d.VerifiedBy)
                .HasConstraintName("FK_Donations_VerifiedBy");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC070EE7DE8B");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_Users");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC079E2EDAC1");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E4A0103701").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534BA0634EC").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.ProfileImage).HasMaxLength(500);
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
