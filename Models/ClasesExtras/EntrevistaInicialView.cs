using Plataforma_Web.Models.PrimeraEntrevista.Secundarios;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Web;

namespace Plataforma_Web.Models
{
    public class EntrevistaInicialView 
    {
        //====Checar como agregar lo de ing y tsu como separa lo que no quiere que se vea

        //Aspectos Economico
        [Key]
        public int IdEntrevistaInicial { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime Fecha { get; set; }

        [Required]
        [DisplayName("Matricula")]
        public int IdMatricula { get; set; }
        [NotMapped]
        public string Matricula { get; set; }

        [NotMapped]
        public string Nombre { get; set; }

        [NotMapped]
        public int Edad { get; set; }

        [NotMapped]
        public string Grupo { get; set; }

        [NotMapped]
        public string Direccion { get; set; }

        [NotMapped]
        public string Celular { get; set; }

        [NotMapped]
        public string Telefono { get; set; }

        [NotMapped]
        public string TelEmergencia { get; set; }

        [NotMapped]
        public string Email { get; set; }

        [Required]
        [DisplayName("¿Resides en esta ciudad?")]
        public bool CiudadOP { get; set; }

        [DisplayName("¿Especificas dónde?")]
        public string Ciudad { get; set; }

        [Required]
        [DisplayName("¿Con quién vives actualmente?")]
        public string Familiar { get; set; }

        [Required]
        [DisplayName("¿Trabajas?")]
        public bool TrabajaOP { get; set; }


        [DisplayName("Especifica dónde trabajas:")]
        public string Trabaja { get; set; }

        //[Required]
        //[DisplayName("Horario de trabajo:")]
        //public string Horario { get; set; }

        [Required]
        [DisplayName("¿Tienes dependientes economicos?")]
        public bool DependienteOP { get; set; }

        [DisplayName("Especifica cuantos dependientes economicos tienes")]
        public string Dependiente { get; set; }

        [Required]
        [DisplayName("¿A que se dedica tu papá?")]
        public string OcupacionPapa { get; set; }

        [Required]
        [DisplayName("¿A que se dedica tu mamá?")]
        public string OcupacionMama { get; set; }

        [Required]
        [DisplayName("Si tienes hermanos señala cuanto son:")]
        public string CantidadHermano { get; set; }

        [Required]
        [DisplayName("¿Cual es el ingreso familiar mensual aproximado?")]
        public string IngresoM { get; set; }

        //============================================= 
        //Aspecto Academico
        [Required]
        [DisplayName("¿De que bachillerato egresaste?")]
        public string Bachillerato { get; set; }

        [DisplayName("Especialidad:")]
        public string Especialidad { get; set; }

        [Required]
        [DisplayName("Promedio:")]
        public string Promedio { get; set; }

        [Required]
        [DisplayName("¿Qué materias se te dificultan más?")]
        public string MateriasDif { get; set; }

        [Required]
        [DisplayName("¿Utilizas alguna técnica de estudio?")]
        public bool TecnicaEstOP { get; set; }

        [DisplayName("¿Cúal?")]
        public string TecnicaEst { get; set; }

        //====================================
        //Aspecto Personal
        [Required]
        [DisplayName("¿Estas casado?")]
        public bool Casado { get; set; }

        [Required]
        [DisplayName("¿Tienes Hijos?")]
        public bool TieneHijo { get; set; }

        [DisplayName("¿Cuantos?")]
        public string CantidadHijo { get; set; }

        [Required]
        [DisplayName("¿Padece alguna enfermedad o alergia?")]
        public bool PadeceEnfermedad { get; set; }

        [DisplayName("¿Especifica?")]
        public string Especifica { get; set; }

        [Required]
        [DisplayName("¿Fumas?")]
        public bool Fuma { get; set; }

        [DisplayName("¿Especifica cantidad y frecuencia?")]
        public string CantidadFuma { get; set; }

        [Required]
        [DisplayName("¿Ingerias bebidad alcohólicas?")]
        public bool IngereBebidaAlcoholica { get; set; }

        [DisplayName("¿Especifica cantidad y frecuencia?")]
        public string CantidadBedida { get; set; }

        [Required]
        [DisplayName("¿Has pensado que la vida no tiene sentido?")]
        public bool VidaSinSentido { get; set; }

        [DisplayName("Porque:")]
        public string Porque { get; set; }

        //ID observacion familia
        [Required]
        [DisplayName("En tu familia has observado que hay: ")]
        public int IdObservacion { get; set; }
        [NotMapped]
        public string Observacion { get; set; }

        [Required]
        [DisplayName("¿Consideras que el apoyo de tu familia cuando tienes un problema es el adecuado?")]
        public string ApoyoFamiliaEnProblemas { get; set; }

        [DisplayName("Porque:")]
        public string ApoyoFamiliaEnProblemasPorque { get; set; }

        [Required]
        [DisplayName("¿Los problemas economicos de tu familia te afecta o te desconcentran?")]
        public string ProblemasEconomicosFamilia { get; set; }

        [DisplayName("Porque:")]
        public string ProblemasEconomicosFamiliaPorque { get; set; }

        //Seleccion a ser llenada por el Tutor del grupo
        //===============================


        [DisplayName("Primer Area")]
        public string Area1{ get; set; }

        [DisplayName("Segunda Area")]
        public string Area2 { get; set; }

        [DisplayName("Tercer Area")]
        public string Area3 { get; set; }

        [DisplayName("Cuarta Area")]
        public string Area4 { get; set; }

        [DisplayName("Nivel de desempeño1")]
        public string NivelDesempeño1 { get; set; }

        [DisplayName("Nivel de desempeño2")]
        public string NivelDesempeño2 { get; set; }

        [DisplayName("Nivel de desempeño3")]
        public string NivelDesempeño3 { get; set; }

        [DisplayName("Nivel de desempeño4")]
        public string NivelDesempeño4 { get; set; }


        [DisplayName("Resultado de la evaluación psicométrica")]
        public string EvaluacionPsicometrica { get; set; }

        [DisplayName("¿Se considera al alumno vulnerable?")]
        public int IdVulnerable { get; set; }
        public virtual Respuesta10 Respuesta10 { get; set; }

        //Checar como poner en opciones
        [DisplayName("Marque los grupos en los que considera al alumno como vulnerable")]
        public int IdEleccionVunerabilidad { get; set; }
        public virtual Vulnerable Vulnerable { get; set; }
        [NotMapped]
        public string TipoVulnerabilidad { get; set; }


        [DisplayName("Resultado del Examen PROPEDÉUTICO")]
        public int IdResultadoPropedeutico { get; set; }
        [NotMapped]
        public string ResultadoPropedeutico { get; set; }


        //==================Checar como subir la foto

        public string Foto { get; set; }

        [Display(Name = "Foto")]
        public HttpPostedFileBase FotoFile { get; set; }


        //Se realaciona con Entrevista inicial
        //public int IdEntrevistaInicial { get; set; };
        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }
    }
}