using Plataforma_Web.Models.ClasesExtras;
using Plataforma_Web.Models.ClasesPAT;
using PlataformaWeb.BecasTransporte.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class TutoriaGrupal
    {
        [Key]
        public int IdTutoriaGrupal { get; set; }

        [Required]
        [DisplayName("Grado")]
        public int IdGrado { get; set; }
        public virtual Grado Grado { get; set; }

        [Required]
        [DisplayName("Grupo")]
        public int IdGrupo { get; set; }
        public virtual Grupo Grupo { get; set; }

        [Required]
        [DisplayName("Carrera")]
        public int IdCarrera { get; set; }
        public virtual Carrera Carrera { get; set; }

        [Required(ErrorMessage = "Debe seleccionar una especialidad")]
        [DisplayName("Especialidad")]
        public int? IdEspecialidad { get; set; }

        [ForeignKey("IdEspecialidad")]
        public virtual Especialidad Especialidad { get; set; }

        [Required]
        [DisplayName("Tutor")]
        public int IdUsuario { get; set; }
        public virtual Usuario Usuario { get; set; }

        [Required]
        [DisplayName("Turno")]
        public int IdTurno { get; set; }
        public virtual Turno Turno { get; set; }

        [Required]
        [DisplayName("Periodo")]
        public int IdPeriodo { get; set; }
        public virtual Periodo Periodo { get; set; }

        [DisplayName("Año")]
        public int Año { get; set; }

        [NotMapped]
        public string Nomenclatura { get; set; }
    }
}