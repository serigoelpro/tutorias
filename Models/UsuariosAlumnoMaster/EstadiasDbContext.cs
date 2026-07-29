using System.Data.Entity;

namespace Plataforma_Web.Models.UsuariosAlumnoMaster
{
    public class EstadiasDbContext : DbContext
    {
        public EstadiasDbContext() : base("estadias_model_db")
        {
        }

        public DbSet<UsuarioEstadias> Usuario1 { get; set; }
        public DbSet<CarreraEstadias> Carrera { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UsuarioEstadias>().ToTable("Usuario1");
            modelBuilder.Entity<CarreraEstadias>().ToTable("Carrera");
            base.OnModelCreating(modelBuilder);
        }
    }
}