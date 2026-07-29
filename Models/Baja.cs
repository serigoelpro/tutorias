using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PlataformaWeb.Models
{
    [Table("BajasAlumnos")]
    public class Baja
    {
        [Key]
        public int IdBaja { get; set; }

        [DisplayName("Persona")]
        public int IdPersona { get; set; }

        public int IdEntrevistaInicial { get; set; }

        [Required]
        [DisplayName("Número de folio:")]
        public string Folio { get; set; }

        [DisplayName("Fecha:")]
        [DataType(DataType.Date)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime Fecha { get; set; }


        [DisplayName("Nombre del alumno:")]
        public string Nombre { get; set; }

        [DisplayName("No. de matrícula:")]
        public string Matricula { get; set; }

        [DisplayName("Grupo:")]
        public string Grupo { get; set; }//nomenclatura

        [DisplayName("Carrera:")]
        public string Carrera { get; set; }


        [DisplayName("Area:")]
        public string Area { get; set; }


        [DisplayName("Área:")]
        public string Especialidad { get; set; }

        [DisplayName("Cuatrimestre:")]
        public string Cuatrimestre { get; set; }//periodo

        [DisplayName("Turno:")]
        public string Turno { get; set; }

        [Required]
        [DisplayName("¿El alumno pertenece a algún grupo altamente vulnerable?")]
        public string Vulnerable { get; set; }

        [Required]
        [DisplayName("En caso de pertenecer a un grupo de vulnerabilidad, escriba el tipo de vulnerabilidad:")]
        public string Vulnerabilidad { get; set; }

        [Required]
        [DisplayName("Categoría de la baja del alumno:")]
        public string Causa { get; set; }

        [Required(ErrorMessage = "El campo Causa de la baja es obligatorio.")]
        [DisplayName("Causa de la baja:")]
        public string Otra { get; set; }

        [DisplayName("Tipo de Baja:")]
        public string Tipo { get; set; }

        [Required(ErrorMessage = "El campo Observaciones es obligatorio.")]
        [DisplayName("Observaciones:")]
        public string Observacion { get; set; }


        public bool? Activo { get; set; }


        [DisplayName("Tramitado por:")]
        public string RealizadoPor { get; set; }

        [DisplayName("Reingreso")]
        public int Reingreso { get; set; }

        [DisplayName("Tutor al momento de la baja:")]
        public string NombreTutor { get; set; }
    }
}