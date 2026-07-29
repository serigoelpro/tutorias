using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace PlataformaWeb.Models.Materias
{
    public class MateriaAlumno
    {
        [Key]
        public int Id { get; set; }
        public int IdMateria { get; set; }
        public int IdPersona { get; set; }
        public string NombreMateria { get; set; }
        public string NombreAlumno { get; set; }
        public string Matricula { get; set; }
        public decimal? Calificacion { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public string Observaciones { get; set; }
        public int IntentosExtraordinarios { get; set; } = 0;
        public DateTime? FechaExamenExtraordinario { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
        public DateTime FechaActualizacion { get; set; } = DateTime.Now;

        // Propiedades de estado de materia (existentes)
        [NotMapped]
        public bool MateriaActiva { get; set; } = true;

        [NotMapped]

        public int IdGrado { get; set; }  // Cuatrimestre de la materia

        public string EstadoMateria { get; set; } = "Activa";

        [NotMapped]
        public int NumeroUnidades { get; set; }

        [NotMapped]
        public List<CalificacionUnidad> CalificacionesUnidades { get; set; } = new List<CalificacionUnidad>();

        // ✅ MÉTODO PARA CALCULAR PORCENTAJE DE AVANCE
        public decimal PorcentajeAvance()
        {
            if (NumeroUnidades == 0) return 0;

            int unidadesCalificadas = CalificacionesUnidades?.Count(u => u.Calificacion.HasValue) ?? 0;

            return Math.Round((decimal)unidadesCalificadas / NumeroUnidades * 100, 1);
        }

        // ✅ NUEVAS PROPIEDADES PARA PLAN DE ESTUDIO
        [NotMapped]
        public int IdPlanEstudio { get; set; }

        [NotMapped]
        public string NombrePlan { get; set; } = "Sin plan";

        [NotMapped]
        public int AñoPlan { get; set; } = 0;

        [NotMapped]
        public decimal CalificacionMinimaPlan { get; set; } = 7.0m;

        [NotMapped]
        public bool PermiteDecimales { get; set; } = true;

        [NotMapped]
        public string DescripcionPlan { get; set; } = "";

        // ✅ PROPIEDADES CALCULADAS SEGÚN EL PLAN
        [NotMapped]
        public string TipoCalificacionPlan
        {
            get
            {
                return PermiteDecimales ? "Permite decimales" : "Solo enteros (redondea)";
            }
        }

        [NotMapped]
        public string RequisitoAprobacionPlan
        {
            get
            {
                return CalificacionMinimaPlan.ToString("0.00") + " puntos mínimos";
            }
        }

        [NotMapped]
        public string LabelPlan
        {
            get
            {
                if (AñoPlan >= 2024) return "label-success";
                if (AñoPlan >= 2020) return "label-warning";
                return "label-info";
            }
        }

        [NotMapped]
        public string InfoCompletaPlan
        {
            get
            {
                return $"{NombrePlan} ({AñoPlan}) - {TipoCalificacionPlan} - {RequisitoAprobacionPlan}";
            }
        }

        // ✅ MÉTODO PARA VALIDAR CALIFICACIÓN SEGÚN EL PLAN ESPECÍFICO
        public ResultadoValidacionMateria ValidarCalificacionSegunPlan(decimal? calificacion)
        {
            var resultado = new ResultadoValidacionMateria();

            if (!calificacion.HasValue)
            {
                resultado.CalificacionAjustada = null;
                resultado.EstadoSugerido = "Pendiente";
                resultado.EsValida = true;
                resultado.Mensaje = "Sin calificación";
                return resultado;
            }

            decimal calif = calificacion.Value;

            // Validar rango
            if (calif < 0 || calif > 10)
            {
                resultado.EsValida = false;
                resultado.MensajeError = "La calificación debe estar entre 0 y 10";
                return resultado;
            }

            // Aplicar reglas del plan
            if (!PermiteDecimales)
            {
                // ✅ PLAN 2020: REDONDEO ESPECIAL
                if (calif < 8.0m)
                {
                    // Del 7 para abajo: redondear hacia ABAJO (floor)
                    resultado.CalificacionAjustada = Math.Floor(calif);
                    resultado.FueAjustada = Math.Abs(calif - resultado.CalificacionAjustada.Value) > 0.001m;
                    resultado.Mensaje = resultado.FueAjustada
                        ? $"Redondeada hacia abajo de {calif} a {resultado.CalificacionAjustada} ({NombrePlan} - calificaciones <8 van hacia abajo)"
                        : $"Procesada: {resultado.CalificacionAjustada} ({NombrePlan})";
                }
                else
                {
                    // Del 8 para arriba: redondear NORMAL
                    resultado.CalificacionAjustada = Math.Round(calif, 0);
                    resultado.FueAjustada = Math.Abs(calif - resultado.CalificacionAjustada.Value) > 0.001m;
                    resultado.Mensaje = resultado.FueAjustada
                        ? $"Redondeada de {calif} a {resultado.CalificacionAjustada} ({NombrePlan} - calificaciones ≥8 redondeo normal)"
                        : $"Procesada: {resultado.CalificacionAjustada} ({NombrePlan})";
                }
            }
            else
            {
                // Plan 2024: Mantener decimales (hasta 2 decimales)
                resultado.CalificacionAjustada = Math.Round(calif, 2);
                resultado.FueAjustada = Math.Abs(calif - resultado.CalificacionAjustada.Value) > 0.001m;
                resultado.Mensaje = resultado.FueAjustada
                    ? $"Redondeada de {calif:0.00} a {resultado.CalificacionAjustada:0.00} ({NombrePlan})"
                    : $"Procesada: {resultado.CalificacionAjustada:0.00} ({NombrePlan})";
            }

            // Determinar estado según calificación mínima del plan
            if (resultado.CalificacionAjustada >= CalificacionMinimaPlan)
            {
                resultado.EstadoSugerido = "Acreditada";
                resultado.EsAprobatoria = true;
                resultado.Mensaje += $" - APROBADO (mín: {CalificacionMinimaPlan})";
            }
            else
            {
                resultado.EstadoSugerido = "Extraordinario";
                resultado.EsAprobatoria = false;
                resultado.Mensaje += $" - NO ACREDITADO (mín: {CalificacionMinimaPlan})";
            }

            resultado.EsValida = true;
            return resultado;
        }

        // ✅ ESTADO DINÁMICO BASADO EN EL PLAN ESPECÍFICO
        [NotMapped]
        public string EstadoDinamicoSegunPlan
        {
            get
            {
                if (!Calificacion.HasValue || Calificacion.Value == 0)
                    return "Pendiente";

                var validacion = ValidarCalificacionSegunPlan(Calificacion);
                return validacion.EstadoSugerido;
            }
        }

        [NotMapped]
        public string EstadoDinamicoLabelSegunPlan
        {
            get
            {
                switch (EstadoDinamicoSegunPlan)
                {
                    case "Acreditada": return "label-success";
                    case "Extraordinario": return "label-warning";
                    default: return "label-default";
                }
            }
        }

        // Propiedades existentes del modelo original (mantener todas)...
        [NotMapped]
        public string ClaseEstadoMateria
        {
            get
            {
                return MateriaActiva ? "materia-activa" : "materia-desactivada";
            }
        }

        [NotMapped]
        public string IconoEstadoMateria
        {
            get
            {
                return MateriaActiva ? "glyphicon-ok-circle text-success" : "glyphicon-ban-circle text-warning";
            }
        }

        [NotMapped]
        public string TextoEstadoMateria
        {
            get
            {
                return MateriaActiva ? "Materia Activa" : "Materia Desactivada";
            }
        }

        [NotMapped]
        public bool RequiereAtencionEspecial
        {
            get
            {
                return !MateriaActiva && (Estado == "Reprobada" || Estado == "Extraordinario");
            }
        }

        [NotMapped]
        public string ClaseFilaTabla
        {
            get
            {
                if (!MateriaActiva)
                    return "materia-desactivada-fila";

                switch (Estado?.ToLower())
                {
                    case "reprobada":
                        return "danger";
                    case "extraordinario":
                        return "warning";
                    case "acreditada":
                        return "success";
                    default:
                        return "";
                }
            }
        }

        // ✅ VALIDACIÓN MEJORADA QUE CONSIDERA EL PLAN
        public bool ValidarCalificacion()
        {
            if (!Calificacion.HasValue) return true;

            var validacion = ValidarCalificacionSegunPlan(Calificacion);
            return validacion.EsValida;
        }

        public string ObtenerMensajeValidacion()
        {
            if (!Calificacion.HasValue) return string.Empty;

            var validacion = ValidarCalificacionSegunPlan(Calificacion);

            if (!validacion.EsValida)
                return validacion.MensajeError;

            if (Estado == "Acreditada" && !validacion.EsAprobatoria)
                return $"Materias acreditadas requieren calificación mínima de {CalificacionMinimaPlan} según {NombrePlan}";

            if (Estado == "Extraordinario" && IntentosExtraordinarios > 3)
                return "Se ha excedido el límite de intentos extraordinarios (máximo 3)";

            if (Estado == "Reprobada" && ArrastreFueraDeTiempo)
                return "La materia está fuera del tiempo límite para arrastre";

            return validacion.Mensaje;
        }

        // Mantener todas las propiedades existentes del modelo original...
        [NotMapped]
        public DateTime FechaInicioArrastreAutomatica
        {
            get
            {
                return FechaInicioArrastre ?? DateTime.Now;
            }
        }

        [NotMapped]
        public DateTime FechaExamenAutomatica
        {
            get
            {
                return FechaExamenExtraordinario ?? DateTime.Now;
            }
        }

        [NotMapped]
        public DateTime? FechaLimiteArrastre
        {
            get
            {
                if (FechaInicioArrastre.HasValue && Estado == "Reprobada")
                {
                    return FechaInicioArrastre.Value.AddMonths(8);
                }
                return null;
            }
        }

        [NotMapped]
        public int DiasRestantesArrastre
        {
            get
            {
                if (FechaLimiteArrastre.HasValue && Estado == "Reprobada")
                {
                    var diasRestantes = (FechaLimiteArrastre.Value - DateTime.Now).Days;
                    return Math.Max(0, diasRestantes);
                }
                return 0;
            }
        }

        [NotMapped]
        public string EstadoTiempoArrastre
        {
            get
            {
                if (Estado != "Reprobada") return "No aplica";

                var dias = DiasRestantesArrastre;

                if (dias <= 0) return "Fuera de tiempo";
                if (dias <= 30) return "Crítico";
                if (dias <= 60) return "En riesgo";
                return "En tiempo";
            }
        }

        [NotMapped]
        public string EstadoTiempoLabel
        {
            get
            {
                switch (EstadoTiempoArrastre)
                {
                    case "Fuera de tiempo": return "label-danger";
                    case "Crítico": return "label-danger";
                    case "En riesgo": return "label-warning";
                    case "En tiempo": return "label-info";
                    default: return "label-default";
                }
            }
        }

        [NotMapped]
        public bool Acreditada => Estado == "Acreditada";

        [NotMapped]
        public bool EsExtraordinario => Estado == "Extraordinario";

        [NotMapped]
        public bool EsReprobada => Estado == "Reprobada";

        [NotMapped]
        public bool ArrastreFueraDeTiempo => DiasRestantesArrastre <= 0 && Estado == "Reprobada";

        [NotMapped]
        public bool ArrastreEnRiesgo => DiasRestantesArrastre <= 60 && DiasRestantesArrastre > 30 && Estado == "Reprobada";

        [NotMapped]
        public bool ArrastreCritico => DiasRestantesArrastre <= 30 && DiasRestantesArrastre > 0 && Estado == "Reprobada";

        [NotMapped]
        public string EstadoTexto
        {
            get
            {
                if (Estado == "Acreditada") return "Acreditada";
                if (Estado == "Reprobada") return "Reprobada";
                if (Estado == "Extraordinario") return "Extraordinario";
                return "Pendiente";
            }
        }

        [NotMapped]
        public string EstadoLabel
        {
            get
            {
                if (Estado == "Acreditada") return "label-success";
                if (Estado == "Reprobada") return "label-danger";
                if (Estado == "Extraordinario") return "label-danger";
                return "label-default";
            }
        }

        [NotMapped]
        public string InformacionCompleta
        {
            get
            {
                var info = $"{NombreMateria} - {EstadoTexto}";

                if (Calificacion.HasValue)
                    info += $" ({Calificacion.Value:0.0})";

                if (Estado == "Extraordinario")
                    info += $" - Intento {IntentosExtraordinarios}";

                if (Estado == "Reprobada" && FechaLimiteArrastre.HasValue)
                    info += $" - Límite: {FechaLimiteArrastre.Value:dd/MM/yyyy}";

                if (!MateriaActiva)
                    info += " - DESACTIVADA";

                // ✅ AGREGAR INFORMACIÓN DEL PLAN
                info += $" - {NombrePlan}";

                return info;
            }
        }

        // Métodos de mantenimiento
        public void ActualizarEstadoBasadoEnCalificacion()
        {
            if (!Calificacion.HasValue) return;

            var validacion = ValidarCalificacionSegunPlan(Calificacion);
            if (validacion.EsValida)
            {
                if (validacion.EsAprobatoria)
                {
                    Estado = "Acreditada";
                }
                else if (Estado == "Pendiente")
                {
                    Estado = "Extraordinario";
                    IntentosExtraordinarios = 1;
                    FechaExamenExtraordinario = DateTime.Now;
                }
                else if (Estado == "Extraordinario" && IntentosExtraordinarios >= 3)
                {
                    Estado = "Reprobada";
                    FechaInicioArrastre = DateTime.Now;
                }
            }
        }

        public void InicializarFechasAutomaticas()
        {
            if (Estado == "Extraordinario" && !FechaExamenExtraordinario.HasValue)
            {
                FechaExamenExtraordinario = DateTime.Now;
            }

            if (Estado == "Reprobada" && !FechaInicioArrastre.HasValue)
            {
                FechaInicioArrastre = DateTime.Now;
            }
        }
    }

    // ✅ CLASE AUXILIAR PARA CALIFICACIONES DE UNIDADES
    public class CalificacionUnidad
    {
        public int Id { get; set; }
        public int IdMateriaAlumno { get; set; }
        public int NumeroUnidad { get; set; }
        public decimal? Calificacion { get; set; }
        public DateTime FechaRegistro { get; set; }
        public DateTime FechaActualizacion { get; set; }
    }

    // ✅ CLASE AUXILIAR PARA RESULTADOS DE VALIDACIÓN DE MATERIA
    public class ResultadoValidacionMateria
    {
        public bool EsValida { get; set; }
        public decimal? CalificacionAjustada { get; set; }
        public string EstadoSugerido { get; set; }
        public bool EsAprobatoria { get; set; }
        public bool FueAjustada { get; set; }
        public string Mensaje { get; set; }
        public string MensajeError { get; set; }
    }

    // Mantener DTOs existentes
    public class MateriaCompletaDto
    {
        public int IdMateria { get; set; }
        public string Nombre { get; set; }
        public int IdCarrera { get; set; }
        public int IdEspecialidad { get; set; }
        public int IdGrado { get; set; }
        public bool Activo { get; set; }
        public decimal? Calificacion { get; set; }
        public string Estado { get; set; }
        public string Observaciones { get; set; }
        public int IntentosExtraordinarios { get; set; }
        public DateTime? FechaExamenExtraordinario { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
    }

}