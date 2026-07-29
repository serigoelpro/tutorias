using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Plataforma_Web.Models
{
    public class DashboardResumenViewModel
    {
        // Estadísticas básicas
        public int TotalGrupos { get; set; }
        public int TotalAlumnos { get; set; }
        public int TotalTutores { get; set; }
        public int EntrevistasCompletadas { get; set; }
        public int EntrevistasPendientes { get; set; }
        public int PatsActivos { get; set; }
        public int AlumnosRiesgo { get; set; }

        // Estadísticas por vulnerabilidad
        public int VulnerabilidadEconomica { get; set; }
        public int VulnerabilidadPersonal { get; set; }
        public int VulnerabilidadAcademica { get; set; }

        // Estadísticas de seguimiento
        public int SeguimientosCompletados { get; set; }
        public int SeguimientosPendientes { get; set; }

        // Estadísticas de bajas
        public int TotalBajas { get; set; }
        public int BajasEsteAno { get; set; }

        // Estadísticas de arrastres
        public int AlumnosConArrastres { get; set; }
        public int MateriasEnArrastre { get; set; }

        // Estadísticas de canalización
        public int CasosCanalizacion { get; set; }
        public int CanalizacionesPendientes { get; set; }

        // Información del usuario actual
        public string NombreUsuario { get; set; }
        public int NivelUsuario { get; set; }
        public string CarreraUsuario { get; set; }
        public string EspecialidadUsuario { get; set; }

        // Filtros aplicados
        public int? CarreraSeleccionada { get; set; }
        public int? EspecialidadSeleccionada { get; set; }
        public int? GrupoSeleccionado { get; set; }
        public int? PeriodoSeleccionado { get; set; }

        // Listas para dropdowns
        public List<CarreraViewModel> Carreras { get; set; }
        public List<EspecialidadViewModel> Especialidades { get; set; }
        public List<GrupoViewModel> Grupos { get; set; }
        public List<PeriodoViewModel> Periodos { get; set; }

        // Actividad reciente
        public List<ActividadRecienteViewModel> ActividadesRecientes { get; set; }

        // Alertas del sistema
        public List<AlertaViewModel> Alertas { get; set; }

        // Estadísticas por período
        public List<EstadisticaPeriodoViewModel> EstadisticasPorPeriodo { get; set; }

        // Gráficas
        public List<GraficaDataViewModel> DatosGraficaEntrevistas { get; set; }
        public List<GraficaDataViewModel> DatosGraficaPATs { get; set; }
        public List<GraficaDataViewModel> DatosGraficaVulnerabilidad { get; set; }

        // Constructor
        public DashboardResumenViewModel()
        {
            Carreras = new List<CarreraViewModel>();
            Especialidades = new List<EspecialidadViewModel>();
            Grupos = new List<GrupoViewModel>();
            Periodos = new List<PeriodoViewModel>();
            ActividadesRecientes = new List<ActividadRecienteViewModel>();
            Alertas = new List<AlertaViewModel>();
            EstadisticasPorPeriodo = new List<EstadisticaPeriodoViewModel>();
            DatosGraficaEntrevistas = new List<GraficaDataViewModel>();
            DatosGraficaPATs = new List<GraficaDataViewModel>();
            DatosGraficaVulnerabilidad = new List<GraficaDataViewModel>();
        }

        // Métodos de cálculo
        public double PorcentajeEntrevistasCompletadas
        {
            get
            {
                if (TotalAlumnos == 0) return 0;
                return Math.Round((double)EntrevistasCompletadas / TotalAlumnos * 100, 2);
            }
        }

        public double PorcentajePATsActivos
        {
            get
            {
                if (TotalAlumnos == 0) return 0;
                return Math.Round((double)PatsActivos / TotalAlumnos * 100, 2);
            }
        }

        public double PorcentajeAlumnosRiesgo
        {
            get
            {
                if (TotalAlumnos == 0) return 0;
                return Math.Round((double)AlumnosRiesgo / TotalAlumnos * 100, 2);
            }
        }

        public string EstadoGeneral
        {
            get
            {
                if (PorcentajeAlumnosRiesgo > 30) return "Crítico";
                if (PorcentajeAlumnosRiesgo > 15) return "Atención";
                if (PorcentajeEntrevistasCompletadas > 80) return "Excelente";
                return "Normal";
            }
        }

        public string ColorEstadoGeneral
        {
            get
            {
                switch (EstadoGeneral)
                {
                    case "Crítico": return "#dc3545";
                    case "Atención": return "#ffc107";
                    case "Excelente": return "#28a745";
                    default: return "#17a2b8";
                }
            }
        }
    }

    // ViewModels auxiliares
    public class CarreraViewModel
    {
        public int IdCarrera { get; set; }
        public string Nombre { get; set; }
        public string Nomenclatura { get; set; }
        public int TotalEstudiantes { get; set; }
        public int TotalTutores { get; set; }
    }

    public class EspecialidadViewModel
    {
        public int Id { get; set; }
        public int IdCarrera { get; set; }
        public string Nombre { get; set; }
        public string CarreraNombre { get; set; }
        public int TotalEstudiantes { get; set; }
    }

    public class GrupoViewModel
    {
        public int IdGrupo { get; set; }
        public string Nombre { get; set; }
        public int TotalEstudiantes { get; set; }
        public string TutorAsignado { get; set; }
    }

    public class PeriodoViewModel
    {
        public int IdPeriodo { get; set; }
        public string Nombre { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public bool Activo { get; set; }
    }

    public class ActividadRecienteViewModel
    {
        public string Tipo { get; set; }
        public string Descripcion { get; set; }
        public DateTime Fecha { get; set; }
        public string Usuario { get; set; }
        public string Icono { get; set; }
        public string Color { get; set; }

        public string FechaFormateada
        {
            get
            {
                var diferencia = DateTime.Now - Fecha;
                if (diferencia.TotalMinutes < 60)
                    return $"Hace {(int)diferencia.TotalMinutes} minutos";
                if (diferencia.TotalHours < 24)
                    return $"Hace {(int)diferencia.TotalHours} horas";
                if (diferencia.TotalDays < 7)
                    return $"Hace {(int)diferencia.TotalDays} días";
                return Fecha.ToString("dd/MM/yyyy");
            }
        }
    }

    public class AlertaViewModel
    {
        public string Tipo { get; set; } // "warning", "danger", "info", "success"
        public string Titulo { get; set; }
        public string Mensaje { get; set; }
        public string Enlace { get; set; }
        public string TextoEnlace { get; set; }
        public DateTime Fecha { get; set; }
        public bool Leida { get; set; }
        public int Prioridad { get; set; } // 1=Alta, 2=Media, 3=Baja

        public string ClaseCSS
        {
            get
            {
                switch (Tipo.ToLower())
                {
                    case "danger": return "alert-danger";
                    case "warning": return "alert-warning";
                    case "success": return "alert-success";
                    default: return "alert-info";
                }
            }
        }

        public string IconoBootstrap
        {
            get
            {
                switch (Tipo.ToLower())
                {
                    case "danger": return "exclamation-triangle-fill";
                    case "warning": return "exclamation-circle-fill";
                    case "success": return "check-circle-fill";
                    default: return "info-circle-fill";
                }
            }
        }
    }

    public class EstadisticaPeriodoViewModel
    {
        public string Periodo { get; set; }
        public int Entrevistas { get; set; }
        public int PATs { get; set; }
        public int Seguimientos { get; set; }
        public int Bajas { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class GraficaDataViewModel
    {
        public string Label { get; set; }
        public double Valor { get; set; }
        public string Color { get; set; }
        public string Descripcion { get; set; }
        public Dictionary<string, object> DatosAdicionales { get; set; }

        public GraficaDataViewModel()
        {
            DatosAdicionales = new Dictionary<string, object>();
        }
    }

    // ViewModels específicos para nuevos módulos
    public class ArrastresViewModel
    {
        public int IdEstudiante { get; set; }
        public string NombreCompleto { get; set; }
        public string Matricula { get; set; }
        public string Carrera { get; set; }
        public string Especialidad { get; set; }
        public string Grupo { get; set; }
        public int Grado { get; set; }
        public List<MateriaArrastreViewModel> MateriasArrastre { get; set; }
        public int TotalMateriasArrastre { get; set; }
        public string EstadoAcademico { get; set; }

        public ArrastresViewModel()
        {
            MateriasArrastre = new List<MateriaArrastreViewModel>();
        }
    }

    public class MateriaArrastreViewModel
    {
        public string NombreMateria { get; set; }
        public int Cuatrimestre { get; set; }
        public double CalificacionAnterior { get; set; }
        public string Estado { get; set; } // "Pendiente", "En Curso", "Aprobada"
        public DateTime FechaRegistro { get; set; }
    }

    public class CanalizacionViewModel
    {
        public int IdBaja { get; set; }
        public string Folio { get; set; }
        public string NombreCompleto { get; set; }
        public string Matricula { get; set; }
        public string Carrera { get; set; }
        public string Especialidad { get; set; }
        public string Grupo { get; set; }
        public string Cuatrimestre { get; set; }
        public string Turno { get; set; }
        public string TipoVulnerabilidad { get; set; }
        public string CausaBaja { get; set; }
        public string TipoBaja { get; set; }
        public string Observaciones { get; set; }
        public DateTime Fecha { get; set; }
        public bool RequiereAtencion { get; set; }
    }

    public class VulnerabilidadViewModel
    {
        public int IdPAT { get; set; }
        public string NombreEstudiante { get; set; }
        public string Matricula { get; set; }
        public string Carrera { get; set; }
        public string Especialidad { get; set; }
        public string Grupo { get; set; }
        public string Tutor { get; set; }
        public int VulnerabilidadEconomica { get; set; }
        public int VulnerabilidadPersonal { get; set; }
        public int VulnerabilidadAcademica { get; set; }
        public string DescripcionEconomica { get; set; }
        public string DescripcionPersonal { get; set; }
        public string DescripcionAcademica { get; set; }
        public DateTime FechaRegistro { get; set; }
        public bool Estado { get; set; }

        public int NivelVulnerabilidadTotal
        {
            get
            {
                return VulnerabilidadEconomica + VulnerabilidadPersonal + VulnerabilidadAcademica;
            }
        }

        public string ClasificacionRiesgo
        {
            get
            {
                var total = NivelVulnerabilidadTotal;
                if (total >= 7) return "Alto";
                if (total >= 4) return "Medio";
                if (total >= 1) return "Bajo";
                return "Sin Riesgo";
            }
        }

        public string ColorRiesgo
        {
            get
            {
                switch (ClasificacionRiesgo)
                {
                    case "Alto": return "#dc3545";
                    case "Medio": return "#ffc107";
                    case "Bajo": return "#17a2b8";
                    default: return "#28a745";
                }
            }
        }
    }

    public class ConcentradoBajasViewModel
    {
        public List<CanalizacionViewModel> Bajas { get; set; }
        public Dictionary<string, int> BajasPorCarrera { get; set; }
        public Dictionary<string, int> BajasPorCuatrimestre { get; set; }
        public Dictionary<string, int> BajasPorTipo { get; set; }
        public Dictionary<string, int> BajasPorMes { get; set; }
        public int TotalBajas { get; set; }
        public int BajasEsteAno { get; set; }
        public int BajasEsteMes { get; set; }
        public double PromedioMensual { get; set; }

        public ConcentradoBajasViewModel()
        {
            Bajas = new List<CanalizacionViewModel>();
            BajasPorCarrera = new Dictionary<string, int>();
            BajasPorCuatrimestre = new Dictionary<string, int>();
            BajasPorTipo = new Dictionary<string, int>();
            BajasPorMes = new Dictionary<string, int>();
        }
    }

    // Enums para mejor tipado
    public enum TipoVulnerabilidad
    {
        [Display(Name = "Sin Vulnerabilidad")]
        SinVulnerabilidad = 0,

        [Display(Name = "Económica")]
        Economica = 1,

        [Display(Name = "Personal")]
        Personal = 2,

        [Display(Name = "Académica")]
        Academica = 3,

        [Display(Name = "Múltiple")]
        Multiple = 4
    }

    public enum NivelRiesgo
    {
        [Display(Name = "Sin Riesgo")]
        SinRiesgo = 0,

        [Display(Name = "Riesgo Bajo")]
        Bajo = 1,

        [Display(Name = "Riesgo Medio")]
        Medio = 2,

        [Display(Name = "Riesgo Alto")]
        Alto = 3,

        [Display(Name = "Riesgo Crítico")]
        Critico = 4
    }

    public enum EstadoPAT
    {
        [Display(Name = "Activo")]
        Activo = 1,

        [Display(Name = "Completado")]
        Completado = 2,

        [Display(Name = "Suspendido")]
        Suspendido = 3,

        [Display(Name = "Cancelado")]
        Cancelado = 4
    }
}
