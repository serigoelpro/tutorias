using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models
{
    [Table("TipoCanalizaciones")]
    public class TipoCanalizaciones
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdTipoCanalizacion { get; set; }

        [Required]
        [StringLength(50)]
        public string Descripcion { get; set; }

        // Relación inversa con Canalizaciones
        public virtual ICollection<Canalizaciones> Canalizaciones { get; set; }
    }
}
