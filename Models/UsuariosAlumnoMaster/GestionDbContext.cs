using System.Data.Entity;

namespace Plataforma_Web.Models.UsuariosAlumnoMaster
{
    public class GestionDbContext : DbContext
    {
        public GestionDbContext() : base("usuarios_model_db")
        {
        }

        public DbSet<AlumnoGestion> Alumnos { get; set; }
        public DbSet<CarreraGestion> Carreras { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlumnoGestion>().ToTable("Alumnos");
            modelBuilder.Entity<CarreraGestion>().ToTable("Carreras");
            base.OnModelCreating(modelBuilder);
        }
    }
}