using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesExtras;
using Plataforma_Web.Models.ClasesPAT;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb.BecasTransporte.Models;
using PlataformaWeb.Models;
using System.Data.Entity;

namespace Plataforma_Web.Data
{
    public class TutoriasContext : DbContext
    {
        public TutoriasContext() : base("tutorias_model_db")
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<TutoriaGrupal> TutoriaGrupals { get; set; }
        public DbSet<Carrera> Carreras { get; set; }
        public DbSet<Grado> Gradoes { get; set; }
        public DbSet<Grupo> Grupoes { get; set; }
        public DbSet<Turno> Turnoes { get; set; }
        public DbSet<Nivel> Nivels { get; set; }
        public DbSet<Periodo> Periodoes { get; set; }
        public DbSet<EntrevistaInicial> EntrevistaInicials { get; set; }

        public DbSet<Estudiante> Estudiantes { get; set; }
        public DbSet<DatosPersonales> DatosPersonales { get; set; }
        public DbSet<Individual> Individuals { get; set; }
        public DbSet<Seguimiento> Seguimientoes { get; set; }
        public DbSet<PlataformaWeb.BecasTransporte.Models.Especialidad> Especialidads { get; set; }
        public DbSet<Vulnerable> Vulnerable { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Configurar la tabla Usuario
            modelBuilder.Entity<Usuario>().ToTable("Usuarios");

            // Relación requerida Usuario -> Nivel
            modelBuilder.Entity<Usuario>()
                .HasRequired(u => u.Nivel)
                .WithMany()
                .HasForeignKey(u => u.IdNivel);

            // Otras tablas (si aplica)
            modelBuilder.Entity<TutoriaGrupal>().ToTable("TutoriaGrupals");
            modelBuilder.Entity<Carrera>().ToTable("Carreras");
            modelBuilder.Entity<Grado>().ToTable("Gradoes");
            modelBuilder.Entity<Grupo>().ToTable("Grupoes");
            modelBuilder.Entity<Turno>().ToTable("Turnoes");
            modelBuilder.Entity<Nivel>().ToTable("Nivels");
            modelBuilder.Entity<Periodo>().ToTable("Periodos");
            modelBuilder.Entity<DatosPersonales>().ToTable("DatosPersonales");
            modelBuilder.Entity<Individual>().ToTable("Individuals");
            modelBuilder.Entity<Seguimiento>().ToTable("Seguimientoes");
            modelBuilder.Entity<PlataformaWeb.BecasTransporte.Models.Especialidad>().ToTable("Especialidads");
            modelBuilder.Entity<Vulnerable>().ToTable("Vulnerables");

            base.OnModelCreating(modelBuilder);
        }
    }

}