using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models
{
    public class Materia
    {
        [Key]
        public int IdMateria { get; set; }

        [Required]
        [Display(Name = "Nombre de la Asignatura")]
        public string Nombre { get; set; }

        [Required]
        [Display(Name = "Carrera")]
        public int IdCarrera { get; set; }

        [Required]
        [Display(Name = "Especialidad")]
        public int IdEspecialidad { get; set; }

        [Required]
        [Display(Name = "Grado")]
        public int IdGrado { get; set; }

        [Required]
        [Display(Name = "Plan de Estudio")]
        public int IdPlanEstudio { get; set; }

        [Required]
        [Range(1, 6, ErrorMessage = "El número de unidades debe estar entre 1 y 6")]
        [Display(Name = "Número de Unidades")]
        public int NumeroUnidades { get; set; }

        [Display(Name = "Activo")]
        public bool Activo { get; set; }

        // Propiedades para mostrar nombres en las vistas (NO ESTÁN EN LA BD)
        [NotMapped]
        [Display(Name = "Carrera")]
        public string NombreCarrera { get; set; }

        [NotMapped]
        [Display(Name = "Especialidad")]
        public string NombreEspecialidad { get; set; }

        [NotMapped]
        [Display(Name = "Grado")]
        public string NombreGrado { get; set; }

        [NotMapped]
        [Display(Name = "Plan de Estudio")]
        public string NombrePlanEstudio { get; set; }

        // Propiedades adicionales del plan de estudio (NO ESTÁN EN LA BD)
        [NotMapped]
        [Display(Name = "Año del Plan")]
        public int? AñoPlan { get; set; }

        [NotMapped]
        [Display(Name = "Calificación Mínima")]
        public decimal? CalificacionMinima { get; set; }

        [NotMapped]
        [Display(Name = "Permite Decimales")]
        public bool? PermiteDecimales { get; set; }

        [NotMapped]
        [Display(Name = "Tipo de Calificación")]
        public string TipoCalificacion
        {
            get
            {
                if (!PermiteDecimales.HasValue) return "N/A";
                return PermiteDecimales.Value ? "Decimales" : "Enteros";
            }
        }

        [NotMapped]
        public string LabelPlan
        {
            get
            {
                if (!AñoPlan.HasValue) return "label-default";
                if (AñoPlan >= 2024) return "label-success";
                if (AñoPlan >= 2020) return "label-warning";
                return "label-info";
            }
        }

        [NotMapped]
        public string RequisitoTexto
        {
            get
            {
                if (!CalificacionMinima.HasValue) return "N/A";
                return CalificacionMinima.Value.ToString("0.00") + " mín.";
            }
        }

        [NotMapped]
        public string PlanCompletoTexto
        {
            get
            {
                if (string.IsNullOrEmpty(NombrePlanEstudio)) return "Sin plan";
                // Ahora el nombre ya es solo el año, no necesitamos agregar nada más
                return $"{NombrePlanEstudio} ({TipoCalificacion}, {RequisitoTexto})";
            }
        }

        // Nueva propiedad calculada para mostrar información de unidades
        [NotMapped]
        public string TextoUnidades
        {
            get
            {
                return NumeroUnidades == 1 ? "1 Unidad" : $"{NumeroUnidades} Unidades";
            }
        }

        [NotMapped]
        public string LabelUnidades
        {
            get
            {
                // Colores según número de unidades
                if (NumeroUnidades <= 2) return "label-info";
                if (NumeroUnidades <= 4) return "label-primary";
                return "label-warning";
            }
        }

        // Método para validar calificación según el plan de estudio
        public bool ValidarCalificacionSegunPlan(decimal calificacion, out decimal calificacionAjustada, out string mensaje)
        {
            calificacionAjustada = calificacion;
            mensaje = "Calificación procesada";

            if (!CalificacionMinima.HasValue || !PermiteDecimales.HasValue)
            {
                mensaje = "Información del plan no disponible";
                return calificacion >= 7; // Default fallback
            }

            // Ajustar según si permite decimales
            if (!PermiteDecimales.Value)
            {
                // Plan viejo (2020): redondear a entero
                calificacionAjustada = Math.Round(calificacion, 0);
                if (Math.Abs(calificacion - calificacionAjustada) > 0.001m)
                {
                    mensaje = $"Calificación redondeada de {calificacion} a {calificacionAjustada} ({NombrePlanEstudio} no permite decimales)";
                }
                else
                {
                    mensaje = $"Calificación procesada: {calificacionAjustada} ({NombrePlanEstudio} no permite decimales)";
                }
            }
            else
            {
                // Plan nuevo (2024): mantener decimales (hasta 2 decimales)
                calificacionAjustada = Math.Round(calificacion, 2);
                mensaje = $"Calificación procesada: {calificacionAjustada:0.00} ({NombrePlanEstudio} permite decimales)";
            }

            // Determinar si es aprobatoria
            bool esAprobatoria = calificacionAjustada >= CalificacionMinima.Value;
            mensaje += esAprobatoria
                ? $" - APROBADO (mínima: {CalificacionMinima.Value})"
                : $" - REPROBADO (mínima: {CalificacionMinima.Value})";

            return esAprobatoria;
        }

        // Información de estado para la interfaz
        [NotMapped]
        public string EstadoTexto
        {
            get
            {
                return Activo ? "Activa" : "Inactiva";
            }
        }

        [NotMapped]
        public string LabelEstado
        {
            get
            {
                return Activo ? "label-success" : "label-danger";
            }
        }
    }
}