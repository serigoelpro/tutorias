using Plataforma_Web.Models.PrimeraEntrevista.Secundarios;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models.PrimeraEntrevista
{
    public class AspectosEconomicos
    {
        [Key]
        public int IdEconomicos { get; set; }

        [Required]
        public int IdPersona { get; set; }
        public virtual DatosPersonales Personales { get; set; }

        [Required]
        [DisplayName("Ciudad de residencia")]
        public int IdCiudad { get; set; }
        public virtual Respuesta1 Respuesta1 { get; set; }

        [Required]
        [DisplayName("Especificar residencia")]
        public string Ciudad { get; set; }


        [DisplayName("Especificar familiar")]
        public string Familiar { get; set; }

        [Required]
        [DisplayName("Trabaja")]
        public int IdTrabajo { get; set; }
        public virtual Respuesta2 Respuesta2 { get; set; }

        [Required]
        [DisplayName("Especifica dónde trabajas:")]
        public string Trabaja { get; set; }

        //[Required]
        //[DisplayName("Horario de trabajo:")]
        //public string Horario { get; set; }


        //[DisplayName("¿Tienes dependientes economicos?")]
        //public int IdDependientes { get; set; }
        //public virtual Respuesta3 Respuesta3 { get; set; }

        [Required]
        [DisplayName("Especifica cuantos dependientes economicos tienes")]
        public string Dependiente { get; set; }

        [Required]
        [DisplayName("Ocupación papá")]
        public string OcupacionPapa { get; set; }

        [Required]
        [DisplayName("Ocupación mamá")]
        public string OcupacionMama { get; set; }

        [Required]
        [DisplayName("Cantidad hermanos:")]
        public string CantidadHermano { get; set; }

        [DisplayName("¿Cual es el ingreso familiar mensual aproximado?")]
        public string IngresoM { get; set; }

        [DisplayName("Beca o apoyo")]
        public string SolicitadoBeca { get; set; }


        [Required]
        [DisplayName("¿Tu situación económica ha afectado tu asistencia, rendimiento o permanencia en la universidad? ")]
        public string AfectacionEco { get; set; }


        [Required]
        [DisplayName("Cantidad de personas en casa")]
        public string CantidadPersonas { get; set; }


        [Required]
        [DisplayName("Cantidad de miembros trabajan")]
        public string CantidadTrabajan { get; set; }

        [Required]
        [DisplayName("Familiar con quien vive")]
        public int IdTipoFamiliar { get; set; }
        public virtual Respuesta16 Respuesta16 { get; set; }

        [Required]
        [DisplayName("Tango de ingreso mensual")]
        public int IdIngresoMes { get; set; }
        public virtual Respuesta17 Respuesta17 { get; set; }


        [DisplayName("Beca o apoyo")]
        public int IdSolicitarBeca { get; set; }
        public virtual Respuesta18 Respuesta18 { get; set; }

    }
}