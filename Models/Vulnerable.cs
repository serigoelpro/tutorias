using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models
{
    [Table("Vulnerables")]
    public class Vulnerable
    {
        [Key]
        public int IdEleccionVunerabilidad { get; set; }

        public string Nombre { get; set; }
    }
}