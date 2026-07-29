using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Plataforma_Web.Models;

namespace PlataformaWeb.Models.Psicologia
{
    [Table("Psicologos")]
    public class Psicologos
    {
        public Psicologos()
        {
            Canalizaciones = new HashSet<Canalizaciones>();
            Psicologo_PsiDetalles = new HashSet<Psicologo_PsiDetalle>();
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdPsicologo { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(255)]
        public string NombreCompleto { get; set; }

        [Required]
        [DefaultValue(true)]
        public bool Activo { get; set; }

        [Required]
        public int IdTipoCanalizacion { get; set; }

        [ForeignKey("PsicologoTurno")]
        public int? IdPsicologoTurno { get; set; }

        [ForeignKey("IdTipoCanalizacion")]
        public virtual TipoCanalizaciones TipoCanalizacion { get; set; }

        public virtual PsicologoTurnos PsicologoTurno { get; set; }

        public virtual ICollection<Canalizaciones> Canalizaciones { get; set; }
        public virtual ICollection<Psicologo_PsiDetalle> Psicologo_PsiDetalles { get; set; } 
    }
}