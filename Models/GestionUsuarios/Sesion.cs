namespace ProyectoIntegracion.Models.GestionUsuarios
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Sesiones")]
    public partial class Sesion
    {
        public Sesion()
        {
            Caducidad = DateTime.Now.AddMinutes(3);
        }

        [Key]
        public int IdSesion { get; set; }

        [Required]
        public string Clave { get; set; }

        [Required]
        public string Valor { get; set; }

        public DateTime Caducidad { get; set; }
    }
}
