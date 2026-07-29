using Plataforma_Web.Models.PrimeraEntrevista.Secundarios;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Plataforma_Web.Models.PrimeraEntrevista
{
    public class AspectosAcademicos
    {
        [Key]
        public int IdAcademicos { get; set; }

        [Required]
        public int IdPersona { get; set; }
        public virtual DatosPersonales Personales { get; set; }

        [Required]
        [DisplayName("Lista de bachillerato")]
        public int IdListaBachillerato { get; set; }
        public virtual Respuesta12 Respuesta12 { get; set; }

        [Required]
        [DisplayName("Especificar bachillerato")]
        public string Bachillerato { get; set; }


        [Required]
        [DisplayName("Especialidad:")]
        public string Especialidad { get; set; }

        [Required]
        [DisplayName("Promedio:")]
        public string Promedio { get; set; }


        [Required]
        [DisplayName("Materia que se dificulta")]
        public string MateriasDif { get; set; }


        [DisplayName("¿Utilizas alguna técnica de estudio?")]
        public int IdTecnicaEst { get; set; }
        public virtual Respuesta0 Respuesta0 { get; set; }

        [DisplayName("¿Cúal?")]
        public string TecnicaEst { get; set; }

        [Required]
        [AllowHtml]
        [DisplayName("Materías no acreditadas")]
        public string MateriasRepro { get; set; }


        [DisplayName("¿Cómo organizas tu tiempo para estudiar y cumplir con tus responsabilidades escolares?")]
        public string TiempoOrg { get; set; }


        [DisplayName("¿Qué tipo de apoyo académico consideras que te ayudaría? ")]
        public string ApoyoAca { get; set; }

        [Required]
        [DisplayName("Rendimiento en clase")]
        public string RendimientoClase { get; set; }

        [Required]
        [DisplayName("Experiencia con profesores")]
        public string ExperienciaProfe { get; set; }

        [Required]
        [DisplayName("Equipo de computo para estudiar")]
        public int IdEquipoComp { get; set; }
        public virtual Respuesta13 Respuesta13 { get; set; }

        [Required]
        [DisplayName("Especifique su dispositivo")]
        public int IdTipoDispositivo { get; set; }
        public virtual Respuesta14 Respuesta14 { get; set; }

        [Required]
        [DisplayName("Servicio de internet")]
        public int IdAccesoInternet { get; set; }
        public virtual Respuesta15 Respuesta15 { get; set; }
    }
}