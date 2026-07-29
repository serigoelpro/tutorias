using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models.Psicologia
{
    [Table("PsiAreasAtencion")]
    public class PsiAreaAtencion
    {
        public PsiAreaAtencion()
        {
            Detalles = new HashSet<PsiDetalleAtencion>();
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdAreaAtencion { get; set; }

        [Required]
        [StringLength(200)]
        public string NombreArea { get; set; }
        public virtual ICollection<PsiDetalleAtencion> Detalles { get; set; }
    }
}