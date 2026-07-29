using Plataforma_Web.Models.PrimeraEntrevista.Secundarios;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models.PrimeraEntrevista
{
    public class AspectosPersonales
    {
        [Key]
        public int IdAspectosPersonales { get; set; }

        [Required]
        public int IdPersona { get; set; }
        public virtual DatosPersonales Personales { get; set; }

        [Required]
        [DisplayName("Casado")]
        public int IdCasado { get; set; }
        public virtual Respuesta4 Respuesta4 { get; set; }

        [Required]
        [DisplayName("Hijos")]
        public int IdHijo { get; set; }
        public virtual Respuesta5 Respuesta5 { get; set; }

        [DisplayName("¿Cuantos?")]
        public string CantidadHijo { get; set; }

        [Required]
        [DisplayName("Enfermedad o alergia")]
        public int IdEnfermedad { get; set; }
        public virtual Respuesta6 Respuesta6 { get; set; }


        [Required]
        [DisplayName("Especifica enfermedad o alergia")]
        public string Especifica { get; set; }

        [Required]
        [DisplayName("Fumas")]
        public int IdFuma { get; set; }
        public virtual Respuesta7 Respuesta7 { get; set; }

        [Required]
        [DisplayName("Especifica cantidad y frecuencia")]
        public string CantidadFuma { get; set; }

        [Required]
        [DisplayName("Ingieres bebidad alcohólicas")]
        public int IdBebida { get; set; }
        public virtual Respuesta8 Respuesta8 { get; set; }

        [Required]
        [DisplayName("Especifica cantidad y frecuencia")]
        public string CantidadBedida { get; set; }

        [Required]
        [DisplayName("Vida no tiene sentido")]
        public int IdVidaSinSentido { get; set; }
        public virtual Respuesta9 Respuesta9 { get; set; }

        [Required]
        [DisplayName("Porque la vida no tiene sentido")]
        public string Porque { get; set; }

        //ID observacion familia
        [Required]
        [DisplayName("En tu familia has observado que hay (violencia):")]
        public int IdObservacionFamilia { get; set; }
        public virtual ObservacioFamilia Observacio { get; set; }

        [Required]
        [DisplayName("Apoyo de familia cuando hay problema es adecuado")]
        public string ApoyoFamiliaEnProblemas { get; set; }

        [Required]
        [DisplayName("Porque apoyo de familia")]
        public string ApoyoFamiliaEnProblemasPorque { get; set; }

        [Required]
        [DisplayName("Problemas economicos de familia afectan o desconcentran")]
        public string ProblemasEconomicosFamilia { get; set; }

        [Required]
        [DisplayName("Porque afectan o desconcentran")]
        public string ProblemasEconomicosFamiliaPorque { get; set; }

        [Required]
        [DisplayName("Ambiente familiar padres/hermanos")]
        public string AmbienteFamiliar { get; set; }


        [Required]
        [DisplayName("Pasatiempos")]
        public string Responsabilidades { get; set; }


        [Required]
        [DisplayName("¿Has atravesado recientemente alguna situación difícil que quieras compartir? ")]
        public string SituacionDif { get; set; }


        [Required]
        [DisplayName("¿Te has sentido triste, ansioso o desmotivado últimamente? ")]
        public string SentidoMal { get; set; }


        [Required]
        [DisplayName("¿Tienes alguien con quien hablar cuando te sientes mal emocionalmente?")]
        public string AlguienHablar { get; set; }


        [Required]
        [DisplayName("¿Conoces los servicios de apoyo psicológico de la universidad? ¿Estarías dispuesto a acudir? ")]
        public string Servicios { get; set; }


        [Required]
        [DisplayName("¿Qué acciones estás dispuesto(a) a realizar para mejorar tu situación? ")]
        public string AccionesMejorar { get; set; }


        [Required]
        [DisplayName("¿En qué podemos apoyarte como institución? ")]
        public string AyudaInstitucion { get; set; }

        [Required]
        [DisplayName("Estado de salud en general")]
        public string SentidoUltimamente { get; set; }


        [Required]
        [DisplayName("¿Hay algo que te gustaría compartir sobre tu situación actual para poder apoyarte mejor?")]
        public string CompartirAlgo { get; set; }

        [Required]
        [DisplayName("Embarazada")]
        public int IdEmbarazo { get; set; }
        public virtual Respuesta11 Respuesta11 { get; set; }

        [Required]
        [DisplayName("Día común")]
        public string DiaComun { get; set; }

        [Required]
        [DisplayName("Gustar de la escuela")]
        public string GustoEscuela { get; set; }

    }
}