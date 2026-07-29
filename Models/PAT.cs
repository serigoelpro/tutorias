using Plataforma_Web.Models.ClasesPAT;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class PAT
    {
        [Key]
        public int IdEntrevistaInicial { get; set; }


        public DateTime Fecha { get; set; }

        [Required]
        [DisplayName("Periodo")]
        public int IdPeriodo { get; set; }
        public virtual Periodo Periodo { get; set; }
        [NotMapped]
        public string Periodos { get; set; }

        [Required]
        [DisplayName("Tutoría grupal de:")]
        public int IdTutoriaGrupal { get; set; }
        [NotMapped]
        public string TutoriaGrupal { get; set; }

        [Required]
        [DisplayName("Carrera")]
        public int IdCarrera { get; set; }
        public virtual Carrera Carrera { get; set; }
        [NotMapped]
        public string Carreras { get; set; }

        [Required]
        [DisplayName("Tutor")]
        public int IdTutor { get; set; }
        public string Tutor { get; set; }

        [Required]
        [DisplayName("Cantidad de Alumnos")]
        public int CantidadAlumno { get; set; }


        [DisplayName("Casos Vulnerables Económicos")]
        public int  VunerableEconomico{ get; set; }

        [DisplayName("Descripción de Casos Vulnerables Económicos")]
        public string DescripcionEconomico { get; set; }


        [DisplayName("Casos Vulnerables Personales")]
        public int VunerablePersonal { get; set; }

        [DisplayName("Descripción de Casos Vulnerables Personales")]
        public string DescripcionPersonal { get; set; }


        [DisplayName("Casos Vulnerables Académicos")]
        public int VunerableAcademico { get; set; }

        [DisplayName("Descripción de Casos Vulnerables Académicos")]
        public string DescripcionAcademico { get; set; }

        [NotMapped]
        public string Cuatrimestre { get; set; }
        [NotMapped]
        public string Grupo { get; set; }
        [NotMapped]
        public string Semana1 { get; set; }
        [NotMapped]
        public string Semana2 { get; set; }
        [NotMapped]
        public string Semana3 { get; set; }
        [NotMapped]
        public string Semana4 { get; set; }
        [NotMapped]
        public string Semana5 { get; set; }
        [NotMapped]
        public string Semana6 { get; set; }
        [NotMapped]
        public string Semana7 { get; set; }      
        [NotMapped]
        public string Semana8 { get; set; }      
        [NotMapped]
        public string Semana9 { get; set; }      
        [NotMapped]
        public string Semana10 { get; set; }     
        [NotMapped]
        public string Semana11 { get; set; }     
        [NotMapped]
        public string Semana12 { get; set; }     
        [NotMapped]
        public string Semana13 { get; set; }     
        [NotMapped]
        public string Semana14 { get; set; }     
        [NotMapped]
        public string Semana15 { get; set; }     
        [NotMapped]
        public string Semana16 { get; set; }     
        [NotMapped]
        public string TipoTutoria1 { get; set; } 
        [NotMapped]
        public string TipoTutoria2 { get; set; }
        [NotMapped]
        public string Actividad { get; set; }
        [NotMapped]
        public string RealizoActividad { get; set; }
        [NotMapped]
        public string Comentarios { get; set; }

        public bool estado { get; set; }
        
        [DisplayName("Estado de revisión")]
        public int EstadoRevision { get; set; } // 0 = No enviado, 1 = Enviado a revisión, 2 = Aprobado, 3 = Rechazado
    }
}