using System;
using System.Data.Entity;

namespace Plataforma_Web.Data
{
    public class EstadiasUTTNContext : DbContext
    {
        public EstadiasUTTNContext() : base("estadias_model_db")
        {
        }

        public DbSet<CarreraEstadias> Carreras { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CarreraEstadias>().ToTable("Carrera");
            modelBuilder.Entity<CarreraEstadias>().HasKey(c => c.IdArea);
            base.OnModelCreating(modelBuilder);
        }
    }

    public class CarreraEstadias
    {
        public int IdArea { get; set; }
        public string Area { get; set; }
        public string CarreraAlumno { get; set; }
        public bool? EsMaestria { get; set; }
    }
}