using System.ComponentModel.DataAnnotations;

namespace PlataformaWeb.Models
{
    public class SeguimientoTutorialViewModel
    {
        [Display(Name = "Grupo")]
        public string Grupo { get; set; }

        [Display(Name = "Nombre del Tutor")]
        public string NombreTutor { get; set; }

        [Display(Name = "Nombre del Período")]
        public string NombrePeriodo { get; set; }

        [Display(Name = "Año")]
        public int Año { get; set; }

        [Display(Name = "Total")]
        public int Total { get; set; }

        [Display(Name = "Hombres")]
        public int H { get; set; }

        [Display(Name = "Mujeres")]
        public int M { get; set; }

        // Vulnerables por tipo
        [Display(Name = "Vulnerable Económico")]
        public int Vulnerable_Economico { get; set; }

        [Display(Name = "Vulnerable Académico")]
        public int Vulnerable_Academico { get; set; }

        [Display(Name = "Vulnerable Personal")]
        public int Vulnerable_Personal { get; set; }

        [Display(Name = "No Vulnerables")]
        public int No_Vulnerables { get; set; }

        // Padres/Madres por sexo
        [Display(Name = "Padres (H)")]
        public int Padres_H { get; set; }

        [Display(Name = "Madres (M)")]
        public int Madres_M { get; set; }

        // Trabajan por sexo
        [Display(Name = "Trabajan (H)")]
        public int Trabajan_H { get; set; }

        [Display(Name = "Trabajan (M)")]
        public int Trabajan_M { get; set; }

        // Becados por sexo
        [Display(Name = "Becados (H)")]
        public int Becados_H { get; set; }

        [Display(Name = "Becados (M)")]
        public int Becados_M { get; set; }

        [Display(Name = "PAT Activo")]
        public int PAT_Activo { get; set; }

        [Display(Name = "PAT Estado Revisión")]
        public string PAT_EstadoRevision { get; set; }

        // Vulnerables Económicos (H y M)
        public int Vulnerable_Economico_H { get; set; }
        public int Vulnerable_Economico_M { get; set; }

        // Vulnerables Personales (H y M)
        public int Vulnerable_Personal_H { get; set; }
        public int Vulnerable_Personal_M { get; set; }

        // Vulnerables Académicos (H y M)
        public int Vulnerable_Academico_H { get; set; }
        public int Vulnerable_Academico_M { get; set; }

        // No Vulnerables (H y M)
        public int No_Vulnerables_H { get; set; }
        public int No_Vulnerables_M { get; set; }

        // Padres de Familias (total)
        public int Padres_Familias { get; set; }

        // Propiedades calculadas para totales
        public int TotalVulnerables => Vulnerable_Economico + Vulnerable_Academico + Vulnerable_Personal;
        public int TotalPadres => Padres_H + Madres_M;
        public int TotalTrabajan => Trabajan_H + Trabajan_M;
        public int TotalBecados => Becados_H + Becados_M;
    }
}
