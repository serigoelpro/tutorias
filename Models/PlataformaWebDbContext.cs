using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesExtras;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb.BecasTransporte.Models;
using System.Data.Entity;

namespace Plataforma_Web.Models
{
    public class PlataformaWebDbContext : DbContext
    {
        public PlataformaWebDbContext() : base("tutorias_model_db")
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<DatosPersonales> DatosPersonales { get; set; }
        public DbSet<Carrera> Carreras { get; set; }
        public DbSet<Especialidad> Especialidads { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Configura la tabla Usuarios
            modelBuilder.Entity<Usuario>().ToTable("Usuarios");
            modelBuilder.Entity<Usuario>().HasKey(u => u.IdUsuario);

            // Configura las propiedades
            modelBuilder.Entity<Usuario>().Property(u => u.IdUsuario)
                .HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity);
            modelBuilder.Entity<Usuario>().Property(u => u.NombreCompleto).IsRequired();
            modelBuilder.Entity<Usuario>().Property(u => u.UserName).IsRequired();
            modelBuilder.Entity<Usuario>().Property(u => u.Password).IsRequired();
            modelBuilder.Entity<Usuario>().Property(u => u.CorreoElectronico).IsRequired();
            modelBuilder.Entity<Usuario>().Property(u => u.IdCarrera).IsRequired();
            modelBuilder.Entity<Usuario>().Property(u => u.IdNivel).IsRequired();
            modelBuilder.Entity<Usuario>().Property(u => u.Estado).IsRequired();
            modelBuilder.Entity<Usuario>().Property(u => u.Tiempo).IsRequired().HasColumnType("datetime");

            // Configura DatosPersonales
            modelBuilder.Entity<DatosPersonales>().ToTable("DatosPersonales");

            // Configura Carreras
            modelBuilder.Entity<Carrera>().ToTable("Carreras");

            modelBuilder.Entity<Especialidad>().ToTable("Especialidads");

            base.OnModelCreating(modelBuilder);
        }
    }
}