using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Plataforma_Web.Models.ClasesPAT
{
    [Table("SemanasPeriodo")]
    public class SemanasPeriodo
    {
        [Key]
        public int IdMaxSemana { get; set; }

        [Required]
        public int IdPeriodo { get; set; }

        [Required]
        public int Año { get; set; }

        [Required]
        [DisplayName("Semanas máximas")]
        public int MaxSemanas { get; set; }

        public int? ModificadoPor { get; set; }

        [Required]
        public DateTime FechaCreacion { get; set; }

        public DateTime? FechaModificacion { get; set; }

        [ForeignKey("IdPeriodo")]
        public virtual Periodo Periodo { get; set; }

        [ForeignKey("ModificadoPor")]
        public virtual Usuario Usuario { get; set; }
    }
}
