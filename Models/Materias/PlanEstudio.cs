using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models
{
    public class PlanEstudio
    {
        [Key]
        public int IdPlanEstudio { get; set; }
        [Required]
        [StringLength(100)]
        [Display(Name = "Año del Plan")]
        public string Nombre { get; set; }

        [Required]
        [Display(Name = "Año")]
        [Range(2000, 2050, ErrorMessage = "El año debe estar entre 2000 y 2050")]
        public int Año { get; set; }

        [Required]
        [Display(Name = "Calificación Mínima")]
        [Range(0, 10, ErrorMessage = "La calificación debe estar entre 0 y 10")]
        public decimal CalificacionMinima { get; set; }

        [Required]
        [Display(Name = "Permite Decimales")]
        public bool PermiteDecimales { get; set; }

        [StringLength(500)]
        [Display(Name = "Descripción")]
        public string Descripcion { get; set; }

        [Display(Name = "Activo")]
        public bool Activo { get; set; }

        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; }

        // Propiedades calculadas para la interfaz (NO están en la BD)
        [NotMapped]
        [Display(Name = "Tipo de Calificación")]
        public string TipoCalificacion
        {
            get
            {
                return PermiteDecimales ? "Permite decimales" : "Solo enteros (redondea)";
            }
        }

        [NotMapped]
        [Display(Name = "Nivel de Exigencia")]
        public string NivelExigencia
        {
            get
            {
                if (CalificacionMinima >= 8) return "Exigente (8+)";
                if (CalificacionMinima >= 7) return "Estándar (7+)";
                return "Básico (< 7)";
            }
        }

        [NotMapped]
        [Display(Name = "Requisito de Aprobación")]
        public string RequisitoAprobacion
        {
            get
            {
                return CalificacionMinima.ToString("0.00") + " puntos mínimos";
            }
        }

        [NotMapped]
        public string LabelClass
        {
            get
            {
                if (Año >= 2024) return "label-success";  // Verde para planes nuevos
                if (Año >= 2020) return "label-warning";  // Amarillo para planes intermedios
                return "label-info";                      // Azul para planes antiguos
            }
        }

        [NotMapped]
        public string RequisitoTexto
        {
            get
            {
                return CalificacionMinima.ToString("0.00") + " mín.";
            }
        }

        // Método para validar y ajustar calificaciones según el plan
        public CalificacionResult ValidarCalificacion(decimal calificacion)
        {
            var result = new CalificacionResult();

            // Ajustar según si permite decimales
            if (!PermiteDecimales)
            {
                // Plan viejo (2020): redondear a entero
                result.CalificacionAjustada = Math.Round(calificacion, 0);
                result.FueAjustada = Math.Abs(calificacion - result.CalificacionAjustada) > 0.001m;
                result.MensajeAjuste = result.FueAjustada
                    ? $"Calificación redondeada de {calificacion} a {result.CalificacionAjustada} ({Nombre} no permite decimales)"
                    : $"Calificación procesada: {result.CalificacionAjustada} ({Nombre} no permite decimales)";
            }
            else
            {
                // Plan nuevo (2024): mantener decimales (hasta 2 decimales)
                result.CalificacionAjustada = Math.Round(calificacion, 2);
                result.FueAjustada = Math.Abs(calificacion - result.CalificacionAjustada) > 0.001m;
                result.MensajeAjuste = result.FueAjustada
                    ? $"Calificación redondeada de {calificacion:0.00} a {result.CalificacionAjustada:0.00} ({Nombre})"
                    : $"Calificación procesada: {result.CalificacionAjustada:0.00} ({Nombre} permite decimales)";
            }

            // Determinar si es aprobatoria
            result.EsAprobatoria = result.CalificacionAjustada >= CalificacionMinima;
            result.MensajeFinal = result.MensajeAjuste +
                (result.EsAprobatoria
                    ? $" - APROBADO (mínima: {CalificacionMinima})"
                    : $" - REPROBADO (mínima: {CalificacionMinima})");

            return result;
        }
    }

    // Clase auxiliar para los resultados de validación de calificaciones
    public class CalificacionResult
    {
        public decimal CalificacionAjustada { get; set; }
        public bool EsAprobatoria { get; set; }
        public bool FueAjustada { get; set; }
        public string MensajeAjuste { get; set; }
        public string MensajeFinal { get; set; }
    }
}