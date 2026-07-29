using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models.Historico
{
    // Cabecera de un corte historico de estadisticas (un snapshot por guardado).
    [Table("EstadisticasHistoricoCorte")]
    public class EstadisticasHistoricoCorte
    {
        [Key]
        public int IdCorte { get; set; }
        public DateTime FechaCorte { get; set; }
        public int NumPeriodo { get; set; }
        public int AnioPeriodo { get; set; }
        public string NombrePeriodo { get; set; }
        public int CreadoPorIdUsuario { get; set; }
        public string CreadoPorNombre { get; set; }
    }
}
