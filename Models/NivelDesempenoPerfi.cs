using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Plataforma_Web.Models
{
    [Table("NivelDesempenoPerfil")]
    public class NivelDesempenoPerfil
    {
        [Key]
        public int IdNivelDesempeno { get; set; }

        [Required]
        [StringLength(50)]
        public string Area { get; set; }

        [Required]
        [StringLength(100)]
        public string NivelDescripcion { get; set; }

        [Required]
        [StringLength(20)]
        public string ColorSemaforo { get; set; }
    }
}