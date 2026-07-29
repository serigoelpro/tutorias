using Plataforma_Web.Models.ClasesExtras;
using Plataforma_Web.Models.PrimeraEntrevista;
using Plataforma_Web.Models.PrimeraEntrevista.Secundarios;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class EntrevistaInicial
    {
        //====Checar como agregar lo de ing y tsu como separa lo que no quiere que se vea

        //Aspectos Economico
        [Key]
        public int IdEntrevistaInicial { get; set; }

        public int IdPersona { get; set; }


        [DataType(DataType.Date)]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime Fecha { get; set; }

        [Required]
        [DisplayName("Matricula")]
        public string Matricula { get; set; }

        [Required]
        [DisplayName("Nombre")]
        public string Nombre { get; set; }

        [Required]
        [DisplayName("Edad")]
        public int Edad { get; set; }

        [Required]
        [DisplayName("Turno")]
        public int IdTurno { get; set; }
        public virtual Turno Turnos { get; set; }
        public string Turno { get; set; }

        [Required]
        [DisplayName("Carrera")]
        public int IdCarrera { get; set; }
        public virtual Carrera Carreras { get; set; }
        public string Carrera { get; set; }

        [Required]
        [DisplayName("Grupo")]
        public int IdGrupo { get; set; }
        public virtual Grupo Grupos { get; set; }
        public string Grupo { get; set; }

        [Required]
        [DisplayName("Grado")]
        public int IdGrado { get; set; }
        public virtual Grado Grados { get; set; }
        public string Grado { get; set; }


        [DisplayName("Direccion")]
        public string Direccion { get; set; }

        [Required]
        [DisplayName("Celular")]
        public string Celular { get; set; }

        [Required]
        [DisplayName("Telefono")]
        public string Telefono { get; set; }

        [Required]
        [DisplayName("Telefono de Emergencia")]
        public string TelEmergencia { get; set; }

        [Required]
        [DisplayName("Correo Electronico")]
        public string Email { get; set; }


        //[Display(Name = "Foto")]
        //public string Foto { get; set; }

        //private string foto { get; set; }
        //[Display(Name = "Foto")]
        //public string Foto { get {
        //        return foto.Replace("../../", "http://201.174.6.168/Tutorias/");
        //    } set {foto = value;} }

        private string foto { get; set; }
        [Display(Name = "Foto")]
        public string Foto
        {
            get
            {
                if (string.IsNullOrEmpty(foto))
                    return string.Empty;

                return foto.Replace("data:image/jpg;base64,", "");
            }
            set { foto = value; }
        }

        [DisplayName("Sexo")]
        public string Sexo { get; set; }

        //Aspectos Academicos 

        [Required]
        [DisplayName("¿De que bachillerato egresaste?")]
        public int IdListaBachillerato { get; set; }
        public virtual Respuesta12 Respuesta12 { get; set; }
        public string ListaBachillerato { get; set; }

        [DisplayName("¿De que bachillerato egresaste?")]
        public string Bachillerato { get; set; }

        [DisplayName("Especialidad bachillerato:")]
        public string Especialidad { get; set; }

        [Required]
        [DisplayName("Promedio general:")]
        public string Promedio { get; set; }

        [Required]
        [DisplayName("¿Qué materias se te dificultan más?")]
        public string MateriasDif { get; set; }


        //[DisplayName("¿Utilizas alguna técnica de estudio?")]
        //public int IdTecnicaEst { get; set; }
        //public virtual Respuesta0 Respuesta0 { get; set; }
        //public string TecnicaEstSiNo { get; set; }

        //[DisplayName("¿Cúal?")]
        //public string TecnicaEst { get; set; }

        //Aspectos Economicos


        [Required]
        [DisplayName("¿Resides en esta ciudad?")]
        public int IdCiudad { get; set; }
        public virtual Respuesta1 Respuesta1 { get; set; }
        public string Ciudad { get; set; }

        [DisplayName("¿Especificas dónde?")]
        public string LugarVive { get; set; }

        [Required]
        [DisplayName("¿Con quién vives actualmente?")]
        public string Familiar { get; set; }

        [Required]
        [DisplayName("¿Trabajas?")]
        public int IdTrabajo { get; set; }
        public virtual Respuesta2 Respuesta2 { get; set; }
        public string PTrabajas { get; set; }

        [DisplayName("Especifica dónde trabajas:")]
        public string Trabaja { get; set; }

        //[Required]
        //[DisplayName("Horario de trabajo:")]
        //public string Horario { get; set; }


        //[DisplayName("¿Tienes dependientes economicos?")]
        //public int IdDependientes { get; set; }
        //public virtual Respuesta3 Respuesta3 { get; set; }
        //public string TDependientes { get; set; }

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


        [DisplayName("¿Cual es el ingreso familiar mensual aproximado?")]
        public string IngresoM { get; set; }

        //Aspectos Personales


        [Required]
        [DisplayName("¿Estas casado?")]
        public int IdCasado { get; set; }
        public virtual Respuesta4 Respuesta4 { get; set; }
        public string Casado { get; set; }

        [Required]
        [DisplayName("¿Tienes Hijos?")]
        public int IdHijo { get; set; }
        public virtual Respuesta5 Respuesta5 { get; set; }
        public string Hijos { get; set; }

        [DisplayName("¿Cuantos?")]
        public string CantidadHijo { get; set; }

        [Required]
        [DisplayName("¿Padece alguna enfermedad o alergia?")]
        public int IdEnfermedad { get; set; }
        public virtual Respuesta6 Respuesta6 { get; set; }
        public string Enfermedad { get; set; }


        [DisplayName("¿Especifica?")]
        public string Especifica { get; set; }

        [Required]
        [DisplayName("¿Fumas?")]
        public int IdFuma { get; set; }
        public virtual Respuesta7 Respuesta7 { get; set; }
        public string Fuma { get; set; }

        [DisplayName("¿Especifica cantidad y frecuencia?")]
        public string CantidadFuma { get; set; }

        [Required]
        [DisplayName("¿Ingerias bebidad alcohólicas?")]
        public int IdBebida { get; set; }
        public virtual Respuesta8 Respuesta8 { get; set; }
        public string Bebida { get; set; }

        [DisplayName("¿Especifica cantidad y frecuencia?")]
        public string CantidadBedida { get; set; }

        [Required]
        [DisplayName("¿Has pensado que la vida no tiene sentido?")]
        public int IdVidaSinSentido { get; set; }
        public virtual Respuesta9 Respuesta9 { get; set; }
        public string VidaSinSentido { get; set; }

        [DisplayName("Porque:")]
        public string Porque { get; set; }

        //ID observacion familia
        [Required]
        [DisplayName("En tu familia has observado que hay: ")]
        public int IdObservacionFamilia { get; set; }
        public virtual ObservacioFamilia Observacio { get; set; }
        public string Observaciones { get; set; }

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


        [Required]
        [DisplayName("¿estas embarazada?")]
        public int IdEmbarazo { get; set; }
        public virtual Respuesta11 Respuesta11 { get; set; }
        public string Embarazo { get; set; }

        // Seccion del tutor 

        [DisplayName("Area 1")]
        public string Area1 { get; set; }
        [DisplayName("Nivel de desempeño 1")]
        public string NivelDesempeño1 { get; set; }
        [DisplayName("Area 2")]
        public string Area2 { get; set; }
        [DisplayName("Nivel de desempeño 2")]
        public string NivelDesempeño2 { get; set; }
        [DisplayName("Area 3")]
        public string Area3 { get; set; }
        [DisplayName("Nivel de desempeño 3")]
        public string NivelDesempeño3 { get; set; }
        [DisplayName("Area 4")]
        public string Area4 { get; set; }
        [DisplayName("Nivel de desempeño 4")]
        public string NivelDesempeño4 { get; set; }

        public string EvaluacionPsicometrica { get; set; }

        public string HabilidadesDeEstudio { get; set; }

        public int IdVulnerable { get; set; }

        public string Vulnerable { get; set; }


        [Required]
        public int IdEleccionVunerabilidad { get; set; }
        public string EleccionVulnerabilidad { get; set; }



        [DisplayName("Carrera")]
        public string CarreraNom { get; set; }//concatenacion de nombre de carrera completo


        [DisplayName("Area")]
        public string Area { get; set; }//especialidad a mostrar

        //Preguntas nuevas

        [DisplayName("¿Cómo te has sentido últimamente dentro y fuera de la universidad?")]
        public string SentidoUltimamente { get; set; }


        [DisplayName("¿Hay algo que te gustaría compartir sobre tu situación actual para poder apoyarte mejor?")]
        public string CompartirAlgo { get; set; }


        [DisplayName("¿Qué materias se te han dificultado más? ")]
        public string MateriasRepro { get; set; }


        [DisplayName("¿Cómo organizas tu tiempo para estudiar y cumplir con tus responsabilidades escolares?")]
        public string TiempoOrg { get; set; }


        [DisplayName("¿Qué tipo de apoyo académico consideras que te ayudaría? ")]
        public string ApoyoAca { get; set; }


        [DisplayName("¿Cuentas con becas o algún tipo de apoyo? ¿Has solicitado alguno?")]
        public string SolicitadoBeca { get; set; }


        [DisplayName("¿Tu situación económica ha afectado tu asistencia, rendimiento o permanencia en la universidad? ")]
        public string AfectacionEco { get; set; }


        [DisplayName("¿Cómo describirías tu ambiente familiar actualmente? ")]
        public string AmbienteFamiliar { get; set; }


        [DisplayName("¿Tienes responsabilidades familiares (hijos, cuidado de personas, etc.) que interfieren con tus estudios? ")]
        public string Responsabilidades { get; set; }


        [DisplayName("¿Has atravesado recientemente alguna situación difícil que quieras compartir? ")]
        public string SituacionDif { get; set; }


        [DisplayName("¿Te has sentido triste, ansioso o desmotivado últimamente? ")]
        public string SentidoMal { get; set; }


        [DisplayName("¿Tienes alguien con quien hablar cuando te sientes mal emocionalmente?")]
        public string AlguienHablar { get; set; }


        [DisplayName("¿Conoces los servicios de apoyo psicológico de la universidad? ¿Estarías dispuesto a acudir? ")]
        public string Servicios { get; set; }


        [DisplayName("¿Qué acciones estás dispuesto(a) a realizar para mejorar tu situación? ")]
        public string AccionesMejorar { get; set; }


        [DisplayName("¿En qué podemos apoyarte como institución? ")]
        public string AyudaInstitucion { get; set; }

        [DisplayName("¿Qué haces en un día común? ")]
        public string DiaComun { get; set; }

        [DisplayName("¿Qué es lo que más te gusta de la escuela? ")]
        public string GustoEscuela { get; set; }

        [DisplayName("¿Cómo sientes que te va en clases? ")]
        public string RendimientoClase { get; set; }

        [DisplayName("¿Cómo te va con tus profesores? ")]
        public string ExperienciaProfe { get; set; }

        [Required]
        [DisplayName("¿Cuántas personas viven en tu casa?")]
        public string CantidadPersonas { get; set; }

        [Required]
        [DisplayName("¿Cuántas personas trabajan?")]
        public string CantidadTrabajan { get; set; }

        [Required]
        [DisplayName("¿Cuentas con equipo de computo para su estudio?")]
        public int IdEquipoComp { get; set; }
        public virtual Respuesta13 Respuesta13 { get; set; }
        public string EquipoComp { get; set; }

        [Required]
        [DisplayName("Especifique su dispositivo")]
        public int IdTipoDispositivo { get; set; }
        public virtual Respuesta14 Respuesta14 { get; set; }
        public string TipoDispositivo { get; set; }

        [Required]
        [DisplayName("¿Cuentas con servicio de internet?")]
        public int IdAccesoInternet { get; set; }
        public virtual Respuesta15 Respuesta15 { get; set; }
        public string AccesoInternet { get; set; }

        [Required]
        [DisplayName("¿Con quien vives en tu casa?")]
        public int IdTipoFamiliar { get; set; }
        public virtual Respuesta16 Respuesta16 { get; set; }
        public string TipoFamiliar { get; set; }

        [Required]
        [DisplayName("Seleccione su ingreso mensual")]
        public int IdIngresoMes { get; set; }
        public virtual Respuesta17 Respuesta17 { get; set; }
        public string IngresoMes { get; set; }

        [Required]
        [DisplayName("¿Necesita solicitar beca?")]
        public int IdSolicitarBeca { get; set; }
        public virtual Respuesta18 Respuesta18 { get; set; }
        public string SolicitarBeca { get; set; }


        // Perfil Psicoemocional y Cognitivo del Estudiante
        [DisplayName("Nivel Autoestima")]
        public int? IdNivelAutoestima { get; set; }
        [ForeignKey("IdNivelAutoestima")]
        public virtual NivelDesempenoPerfil NivelAutoestima { get; set; }

        [DisplayName("Nivel Tamizaje")]
        public int? IdNivelTamizaje { get; set; }
        [ForeignKey("IdNivelTamizaje")]
        public virtual NivelDesempenoPerfil NivelTamizaje { get; set; }

        [DisplayName("Nivel Pensamiento Abstracto")]
        public int? IdNivelPensamientoAbstracto { get; set; }
        [ForeignKey("IdNivelPensamientoAbstracto")]
        public virtual NivelDesempenoPerfil NivelPensamientoAbstracto { get; set; }

        [DisplayName("Aspecto Economico")]
        public int? IdAspectoEconomico { get; set; }
        [ForeignKey("IdAspectoEconomico")]
        public virtual NivelDesempenoPerfil AspectoEcnomico { get; set; }

        [DisplayName("Aspecto Academico")]
        public int? IdAspectoAcademico { get; set; }
        [ForeignKey("IdAspectoAcademico")]
        public virtual NivelDesempenoPerfil AspectoAcademico { get; set; }

        [DisplayName("Aspecto Personal")]
        public int? IdAspectoPersonal { get; set; }
        [ForeignKey("IdAspectoPersonal")]
        public virtual NivelDesempenoPerfil AspectoPersonal { get; set; }
    }
}