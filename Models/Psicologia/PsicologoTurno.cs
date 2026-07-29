using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models.Psicologia
{
    [Table("PsicologoTurnos")]
    public class PsicologoTurnos
    {
        public PsicologoTurnos()
        {
            Psicologos = new HashSet<Psicologos>();
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdPsicologoTurno { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; }

        public virtual ICollection<Psicologos> Psicologos { get; set; }
    }
}