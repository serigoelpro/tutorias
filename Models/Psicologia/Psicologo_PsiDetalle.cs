using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models.Psicologia
{
    [Table("Psicologo_PsiDetalles")]
    public class Psicologo_PsiDetalle
    {
        [Key]
        [Column(Order = 0)]
        [ForeignKey("Psicologo")]
        public int IdPsicologo { get; set; }

        [Key]
        [Column(Order = 1)]
        [ForeignKey("PsiDetalleAtencion")]
        public int IdDetalleAtencion { get; set; }
        public virtual Psicologos Psicologo { get; set; }
        public virtual PsiDetalleAtencion PsiDetalleAtencion { get; set; }
    }
}