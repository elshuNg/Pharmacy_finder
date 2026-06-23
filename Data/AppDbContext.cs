using Microsoft.EntityFrameworkCore;
using PharmacyFinder.API.Models;

namespace PharmacyFinder.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Pharmacy> Pharmacies => Set<Pharmacy>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<PharmacyMedicine> PharmacyMedicines => Set<PharmacyMedicine>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<PharmacyApproval> PharmacyApprovals => Set<PharmacyApproval>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<UserRole>(name: "user_role");
        modelBuilder.HasPostgresEnum<PharmacyStatus>(name: "pharmacy_status");
        modelBuilder.HasPostgresEnum<PrescriptionStatus>(name: "prescription_status");
        modelBuilder.HasPostgresEnum<ApprovalDecision>(name: "approval_decision");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasColumnType("user_role");

        modelBuilder.Entity<Pharmacy>()
            .HasIndex(p => p.LicenseNumber)
            .IsUnique();

        modelBuilder.Entity<Pharmacy>()
            .Property(p => p.Status)
            .HasColumnType("pharmacy_status");

        modelBuilder.Entity<Pharmacy>()
            .Property(p => p.OperatingHours)
            .HasColumnType("jsonb");

        modelBuilder.Entity<Pharmacy>()
            .HasOne(p => p.Owner)
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PharmacyMedicine>()
            .HasIndex(pm => new { pm.PharmacyId, pm.MedicineId })
            .IsUnique();

        modelBuilder.Entity<PharmacyMedicine>()
            .ToTable(t =>
            {
                t.HasCheckConstraint("CK_PharmacyMedicine_Quantity_NonNegative", "\"Quantity\" >= 0");
                t.HasCheckConstraint("CK_PharmacyMedicine_Price_NonNegative", "\"Price\" >= 0");
            });

        modelBuilder.Entity<PharmacyMedicine>()
            .HasOne(pm => pm.Pharmacy)
            .WithMany(p => p.Medicines)
            .HasForeignKey(pm => pm.PharmacyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PharmacyMedicine>()
            .HasOne(pm => pm.Medicine)
            .WithMany()
            .HasForeignKey(pm => pm.MedicineId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Prescription>()
            .Property(p => p.Status)
            .HasColumnType("prescription_status");

        modelBuilder.Entity<Prescription>()
            .HasOne(p => p.Customer)
            .WithMany()
            .HasForeignKey(p => p.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PrescriptionItem>()
            .HasOne(pi => pi.Prescription)
            .WithMany(p => p.Items)
            .HasForeignKey(pi => pi.PrescriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PrescriptionItem>()
            .HasOne(pi => pi.Medicine)
            .WithMany()
            .HasForeignKey(pi => pi.MedicineId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PharmacyApproval>()
            .Property(a => a.Decision)
            .HasColumnType("approval_decision");

        modelBuilder.Entity<PharmacyApproval>()
            .HasOne<Pharmacy>()
            .WithMany()
            .HasForeignKey(a => a.PharmacyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PharmacyApproval>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.AdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
