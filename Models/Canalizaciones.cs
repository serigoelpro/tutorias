using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PlataformaWeb.Models.Psicologia;

namespace PlataformaWeb.Models
{
    [Table("Canalizaciones")]
    public class Canalizaciones
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdCanalizacion { get; set; }

        [Required]
        public int IdPersona { get; set; }

        [Required]
        public int IdUsuario { get; set; }

        [Column(TypeName = "nvarchar(MAX)")]
        public string CorreoTutor { get; set; }


        [Required]
        [Column(TypeName = "text")]
        public string MotivoCanalizacion { get; set; }

        [Column(TypeName = "nvarchar(MAX)")]
        public string VulnerabilidadesPasadas { get; set; }

        [Required]
        public int IdTipoCanalizacion { get; set; }

        [Required]
        [Column(TypeName = "datetime")]
        public DateTime Fecha { get; set; }

        [Column(TypeName = "nvarchar(MAX)")]
        public string Status { get; set; }

        [ForeignKey("IdTipoCanalizacion")]
        public virtual TipoCanalizaciones TipoCanalizaciones { get; set; }


        public int? IdPsicologo { get; set; }

        [ForeignKey("IdPsicologo")]
        public virtual Psicologos Psicologo { get; set; }

    }
}