namespace ProyectoIntegracion.Models.GestionUsuarios
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Administradore
    {
        [Key]
        public int IdAdministrador { get; set; }

        [Required]
        public string Usuario { get; set; }

        [Required]
        public string Contrasena { get; set; }

        public bool Habilitado { get; set; }

        public DateTime? FechaRegistro { get; set; }

        public DateTime? FechaSesion { get; set; }

        public string MicrosoftIdentificator { get; set; }

        public string TokenSesion { get; set; }
    }
}
