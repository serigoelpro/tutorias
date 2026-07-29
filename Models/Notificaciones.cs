using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models
{
    public class Notificaciones
    {
        [Key]
        public int IdNotificacion { get; set; }

        [Required]
        public int IdEstudiante { get; set; }

        public int? IdTutor { get; set; }

        [Required]
        [MaxLength(500)]
        public string Mensaje { get; set; }

        public DateTime FechaEnvio { get; set; }

        public bool Leida { get; set; }

        public int? IdCanalizacion { get; set; }

        [ForeignKey("IdEstudiante")]
        public virtual DatosPersonales Estudiante { get; set; }

        [ForeignKey("IdTutor")]
        public virtual Usuario Tutor { get; set; }

        [ForeignKey("IdCanalizacion")]
        public virtual Canalizaciones Canalizacion { get; set; }
    }
}