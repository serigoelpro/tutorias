using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;

namespace ProyectoIntegracion.Models.GestionUsuarios
{
    public partial class usuarios_model_db : DbContext
    {
        public usuarios_model_db()
            : base("name=usuarios_model_db")
        {
        }

        public virtual DbSet<Administradore> Administradores { get; set; }
        public virtual DbSet<Alumno> Alumnos { get; set; }
        public virtual DbSet<Carrera> Carreras { get; set; }
        public virtual DbSet<Docente> Docentes { get; set; }
        public virtual DbSet<Sesion> Sesiones { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Mapea explícitamente a la tabla "Carrera" (singular)
            modelBuilder.Entity<Carrera>().ToTable("Carrera");

            modelBuilder.Entity<Carrera>()
                .HasMany(e => e.Alumnos)
                .WithRequired(e => e.Carrera)
                .WillCascadeOnDelete(false);
        }
    }
}