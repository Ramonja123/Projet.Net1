using BackendSGH.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore; 
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Client> Clients { get; set; }
    public DbSet<Responsable> Responsables { get; set; } 
    public DbSet<Chambre> Chambres { get; set; }
    public DbSet<TypeChambre> TypeChambres { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Panier> Paniers { get; set; }
    public DbSet<ReservationChambre> ReservationChambres { get; set; }
    public DbSet<ReservationService> ReservationServices { get; set; }
    public DbSet<Facture> Factures { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);


    builder.Entity<Panier>()
        .HasOne(p => p.Facture)
        .WithOne(f => f.Panier)
        .HasForeignKey<Facture>(f => f.PanierId);

    builder.Entity<ApplicationUser>()
        .HasOne(u => u.ClientProfile)
        .WithOne(c => c.ApplicationUser)
        .HasForeignKey<Client>(c => c.ApplicationUserId);

    builder.Entity<ApplicationUser>()
        .HasOne(u => u.ResponsableProfile)
        .WithOne(r => r.ApplicationUser)
        .HasForeignKey<Responsable>(r => r.ApplicationUserId);

    builder.Entity<TypeChambre>().Property(t => t.Tarif).HasColumnType("decimal(18,2)");
    builder.Entity<Service>().Property(s => s.Prix).HasColumnType("decimal(18,2)");
    builder.Entity<Panier>().Property(p => p.Total).HasColumnType("decimal(18,2)");
    builder.Entity<ReservationChambre>().Property(r => r.PrixTotal).HasColumnType("decimal(18,2)");
    builder.Entity<ReservationService>().Property(r => r.Prix).HasColumnType("decimal(18,2)");
    builder.Entity<Facture>().Property(f => f.MontantTotal).HasColumnType("decimal(18,2)");


    builder.Entity<Facture>()
        .HasOne(f => f.Client)
        .WithMany()
        .HasForeignKey(f => f.ClientId)
        .OnDelete(DeleteBehavior.Restrict); 
}
}