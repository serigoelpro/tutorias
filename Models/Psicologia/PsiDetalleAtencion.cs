using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models.Psicologia
{
    [Table("PsiDetallesAtencion")]
    public class PsiDetalleAtencion
    {
        public PsiDetalleAtencion()
        {
            PsicologosQueAtienden = new HashSet<Psicologo_PsiDetalle>();
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdDetalleAtencion { get; set; }

        [ForeignKey("PsiAreaAtencion")]
        public int IdAreaAtencion { get; set; }

        [Required]
        [StringLength(500)]
        public string DescripcionDetalle { get; set; }
        public virtual PsiAreaAtencion PsiAreaAtencion { get; set; }
        public virtual ICollection<Psicologo_PsiDetalle> PsicologosQueAtienden { get; set; }
    }
}