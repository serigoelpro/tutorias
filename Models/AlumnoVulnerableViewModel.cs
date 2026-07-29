using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace PlataformaWeb.Models
{
    public class AlumnoVulnerableViewModel
    {
        public int IdEntrevistaInicial { get; set; }

        [Display(Name = "Matr�cula")]
        public string Matricula { get; set; }

        [Display(Name = "Nombre")]
        public string Nombre { get; set; }

        [Display(Name = "Carrera")]
        public string Carrera { get; set; }

        [Display(Name = "Grupo")]
        public string Grupo { get; set; }

        [Display(Name = "Cuatrimestre")]
        public string Cuatrimestre { get; set; }

        [Display(Name = "Turno")]
        public string Turno { get; set; }

        [Required(ErrorMessage = "Debe seleccionar si es vulnerable")]
        [Display(Name = "�Es vulnerable?")]
        public int IdVulnerable { get; set; }

        [Display(Name = "Tipo de vulnerabilidad")]
        public int IdEleccionVunerabilidad { get; set; }

        [Display(Name = "Nombre de vulnerabilidad")]
        public string NombreVulnerabilidad { get; set; }

        // Listas para dropdowns
        public IEnumerable<SelectListItem> OpcionesVulnerable { get; set; }
        public IEnumerable<SelectListItem> TiposVulnerabilidad { get; set; }

        [Display(Name = "A�o")]
        public int Anio { get; set; }

        [Display(Name = "Sexo")]
        public string Sexo { get; set; }

        [Display(Name = "Es Padre/Madre")]
        public string EsPadre { get; set; }

        [Display(Name = "Trabaja")]
        public string Trabaja { get; set; }

        [Display(Name = "Tutor")]
        public string NombreTutor { get; set; }

        [Display(Name = "Estado PAT")]
        public string EstadoPAT { get; set; }

        [Display(Name = "Estado Revisi�n")]
        public string EstadoRevision { get; set; }

        // Propiedades adicionales para filtros
        public int IdCarrera { get; set; }
        public int IdGrupo { get; set; }
        public int IdGrado { get; set; }
        public int IdTurno { get; set; }
    }
}

