using System;
using System.Data.Entity;
using ProyectoIntegracion.Models.GestionUsuarios;

namespace Plataforma_Web.Data
{
    public class GestionUsuariosContext : DbContext
    {
        public GestionUsuariosContext() : base("usuarios_model_db")
        {
        }

        public DbSet<Alumno> Alumnos { get; set; }

        public DbSet<Docente> Docentes { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Alumno>().ToTable("Alumnos");
            modelBuilder.Entity<Alumno>().HasKey(a => a.IdAlumno);

            base.OnModelCreating(modelBuilder);
        }
    }

    public class Alumno
    {
        public int IdAlumno { get; set; }
        public string Nombre { get; set; }
        public string ApellidoPaterno { get; set; }
        public string ApellidoMaterno { get; set; }
        public string Matricula { get; set; }
        public string Contrasena { get; set; }
        public string CorreoElectronico { get; set; }
        public int IdCarrera { get; set; }
        public int Cuatrimestre { get; set; }
        public bool RegistradoEstadias { get; set; }
        public bool Habilitado { get; set; }
        public DateTime? FechaRegistro { get; set; }
        public DateTime? FechaSesion { get; set; }
        public string MicrosoftIdentifier { get; set; }
        public string TokenSesion { get; set; }
    }
}