using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models.Historico
{
    // Detalle: una fila por seccion del corte, con su JSON agregado.
    [Table("EstadisticasHistoricoSeccion")]
    public class EstadisticasHistoricoSeccion
    {
        [Key]
        public int IdSeccion { get; set; }
        public int IdCorte { get; set; }
        public string Seccion { get; set; }
        public string DatosJson { get; set; }
    }
}
