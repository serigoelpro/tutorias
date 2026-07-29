namespace ProyectoIntegracion.Models.GestionUsuarios
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Docente
    {
        [Key]
        public int IdDocente { get; set; }

        [Required]
        public string Nombre { get; set; }

        [Required]
        public string ApellidoPaterno { get; set; }

        [Required]
        public string ApellidoMaterno { get; set; }

        [Required]
        public string NumeroEmpleado { get; set; }

        [Required]
        public string Matricula { get; set; }

        public string Contrasena { get; set; }

        [Required]
        public string CorreoElectronico { get; set; }

        public bool Autorizado { get; set; }

        public bool Habilitado { get; set; }

        public DateTime? FechaRegistro { get; set; }

        public DateTime? FechaSesion { get; set; }

        public string MicrosoftIdentifier { get; set; }

        public string TokenSesion { get; set; }
    }
}
