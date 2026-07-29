using System.Data.Entity;

namespace Plataforma_Web.Models.UsuariosAlumnoMaster
{
    public class TutoriasDbContext : DbContext
    {
        public TutoriasDbContext() : base("tutorias_model_db")
        {
        }

        public DbSet<UsuarioTutorias> Usuarios { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UsuarioTutorias>().ToTable("Usuarios");
            base.OnModelCreating(modelBuilder);
        }
    }
}