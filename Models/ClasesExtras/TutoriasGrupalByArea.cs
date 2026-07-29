using Plataforma_Web.Models.ClasesExtras;
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
    public class TutoriasGrupalsByArea
    {
        [Key]
        public int IdTutoriaGrupal { get; set; }
        public int? IdPeriodo { get; set; }
        public int? IdUsuario { get; set; }
        public string Periodo { get; set; }
        public int? Año { get; set; }
        public int? IdTurno { get; set; }
        public string Turno { get; set; }
        public string Grado { get; set; }
        public string Grupo { get; set; }
        public int? IdCarrera { get; set; }
        public string Carrera { get; set; }
        public string Especialidad { get; set; }
        public int? IdEspecialidad { get; set; }
        public int AlumnosSoporte { get; set; }
        [NotMapped]
        public string Nomenclatura { get; set; }

        [DisplayName("Sistema Nuevo")]
        public bool? SistemaNuevo { get; set; }
    }
}