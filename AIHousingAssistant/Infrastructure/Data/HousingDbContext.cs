using AIHousingAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIHousingAssistant.Infrastructure.Data
{
    public partial class HousingDbContext : DbContext
    {
        public HousingDbContext(DbContextOptions<HousingDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<HousingUnit> HousingUnits { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HousingUnit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitType).HasMaxLength(50);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
