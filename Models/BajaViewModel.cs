using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PlataformaWeb.Models
{
    public class BajaViewModel
    {
        public int IdPersona { get; set; }
        public int IdEntrevistaInicial { get; set; }

        [DisplayName("Número de folio:")]
        public string Folio { get; set; }

        [DisplayName("Fecha:")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime FechaRegistro { get; set; }

        public string Nombre { get; set; }

        [DisplayName("No. de matrícula:")]
        public string Matricula { get; set; }

        [DisplayName("Grupo:")]
        public string Grupo { get; set; }

        [Required(ErrorMessage = "La carrera es obligatoria.")]
        [DisplayName("Carrera:")]
        public string Carrera { get; set; }


        [DisplayName("Área:")]
        public string Area { get; set; }

        [DisplayName("Especialidad:")]
        public string Especialidad { get; set; }

        [DisplayName("Cuatrimestre:")]
        public string Cuatrimestre { get; set; }

        [DisplayName("Turno:")]
        public string Turno { get; set; }

        [Required(ErrorMessage = "Este campo es obligatorio.")]
        [DisplayName("¿El alumno pertenece a algún grupo altamente vulnerable?")]
        public string Vulnerable { get; set; }

        [Required(ErrorMessage = "Debe especificar el tipo de vulnerabilidad.")]
        [DisplayName("En caso de pertenecer a un grupo de vulnerabilidad, escriba el tipo de vulnerabilidad:")]
        public string Vulnerabilidad { get; set; }

        [Required(ErrorMessage = "Debe especificar la causa de la baja.")]
        [DisplayName("Categoría de la baja del alumno:")]
        public string Causa { get; set; }

        [DisplayName("Causa de la baja:")]
        public string Otra { get; set; }

        [DisplayName("Tipo de Baja:")]
        public string Tipo { get; set; }

        [Required(ErrorMessage = "Debe escribir una observación.")]
        [DisplayName("Observaciones:")]
        public string Observacion { get; set; }

        public bool? Activo { get; set; }

        public string RealizadoPor { get; set; }

        public string ErrorLogico { get; set; }

    }
}
