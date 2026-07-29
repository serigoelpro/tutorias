namespace ProyectoIntegracion.Models.GestionUsuarios
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Alumnos")]
    public partial class Alumno
    {
        [Key]
        public int IdAlumno { get; set; }

        [Required]
        public string Nombre { get; set; }

        [Required]
        public string ApellidoPaterno { get; set; }

        [Required]
        public string ApellidoMaterno { get; set; }

        [Required]
        public string Matricula { get; set; }

        [Required]
        public string Contrasena { get; set; }

        [Required]
        public string CorreoElectronico { get; set; }

        public int IdCarrera { get; set; }

        public int Cuatrimestre { get; set; }

        public bool RegistradoEstadias { get; set; }

        public bool Habilitado { get; set; }

        public DateTime? FechaRegistro { get; set; }

        public DateTime? FechaSesion { get; set; }

        public string MicrosoftIdentifier { get; set; }

        public string TokenSesion { get; set; }

        public virtual Carrera Carrera { get; set; }
    }
}
