using System;
using System.Collections.Generic;

namespace PlataformaWeb.Models.Materias
{
    /// <summary>
    /// Representa un intento individual de aprobar una materia
    /// </summary>
    public class IntentoMateria
    {
        public int Intento { get; set; }
        public string TipoIntento { get; set; } // "Ordinario", "Extraordinario", "Arrastre (Intento N)"
        public decimal CalificacionOriginal { get; set; }
        public decimal CalificacionAjustada { get; set; }
        public bool EsAprobatoria { get; set; }
        public DateTime FechaRegistro { get; set; }
        public string Estado { get; set; } // "Acreditada", "Reprobada", "Extraordinario", "Pendiente"
        public string Observaciones { get; set; }
        public decimal CalificacionMinimaPlan { get; set; }
        public bool PermiteDecimales { get; set; }

        /// <summary>
        /// Label de Bootstrap según el resultado del intento
        /// </summary>
        public string LabelClass
        {
            get
            {
                if (EsAprobatoria) return "label-success";
                return "label-danger";
            }
        }

        /// <summary>
        /// Icono según el resultado del intento
        /// </summary>
        public string IconoResultado
        {
            get
            {
                if (EsAprobatoria) return "glyphicon-ok-circle";
                return "glyphicon-remove-circle";
            }
        }

        /// <summary>
        /// Texto descriptivo del resultado
        /// </summary>
        public string TextoResultado
        {
            get
            {
                if (EsAprobatoria) return "APROBADO";
                return "REPROBADO";
            }
        }

        /// <summary>
        /// Información completa del intento
        /// </summary>
        public string InformacionCompleta
        {
            get
            {
                string info = $"{TipoIntento}: {CalificacionAjustada:0.00} - {TextoResultado}";
                if (CalificacionOriginal != CalificacionAjustada)
                {
                    info += $" (Original: {CalificacionOriginal:0.00})";
                }
                info += $" - {FechaRegistro:dd/MM/yyyy}";
                return info;
            }
        }
    }

    /// <summary>
    /// Historial completo de intentos de una materia para un alumno
    /// </summary>
    public class HistorialMateriaAlumno
    {
        public int IdMateria { get; set; }
        public int IdPersona { get; set; }
        public string NombreMateria { get; set; }
        public List<IntentoMateria> Intentos { get; set; } = new List<IntentoMateria>();

        /// <summary>
        /// Obtiene el estado final de la materia basado en el último intento
        /// </summary>
        public string EstadoFinal
        {
            get
            {
                if (Intentos == null || Intentos.Count == 0)
                    return "Pendiente";

                var ultimoIntento = Intentos[Intentos.Count - 1];
                return ultimoIntento.Estado;
            }
        }

        /// <summary>
        /// Indica si la materia fue aprobada en algún intento
        /// </summary>
        public bool FueAprobada
        {
            get
            {
                if (Intentos == null || Intentos.Count == 0)
                    return false;

                return Intentos.Exists(i => i.EsAprobatoria);
            }
        }

        /// <summary>
        /// Obtiene el intento en el que se aprobó (si aplica)
        /// </summary>
        public IntentoMateria IntentoAprobatorio
        {
            get
            {
                if (Intentos == null || Intentos.Count == 0)
                    return null;

                return Intentos.Find(i => i.EsAprobatoria);
            }
        }

        /// <summary>
        /// Número total de intentos realizados
        /// </summary>
        public int TotalIntentos
        {
            get
            {
                return Intentos?.Count ?? 0;
            }
        }

        /// <summary>
        /// Indica cómo se aprobó la materia (si aplica)
        /// </summary>
        public string FormaAprobacion
        {
            get
            {
                var intentoAprobatorio = IntentoAprobatorio;
                if (intentoAprobatorio == null)
                    return "No aprobada";

                return intentoAprobatorio.TipoIntento;
            }
        }
    }
}