using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PlataformaWeb.Models.Materias
{
    // DTO PARA MAPEAR CON LA VISTA vw_ArrastreCarreraCoordinador
    public class ArrastreCarreraDto
    {
        public int IdPersona { get; set; }
        public int IdMateria { get; set; }
        public string Matricula { get; set; }
        public string NombreAlumno { get; set; }
        public string NombreCarrera { get; set; }
        public string NombreEspecialidad { get; set; }
        public string GradoGrupo { get; set; }
        public string MateriaArrastre { get; set; }
        public int CuatrimestreMateria { get; set; }
        public string CuatrimestreTexto { get; set; }
        public int IntentosExtraordinarios { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
        public string Observaciones { get; set; }
        public string NombreTurno { get; set; }
        public string NombrePeriodo { get; set; }
        public int Año { get; set; }
        public string TutorAsignado { get; set; }
        public int NivelCriticidad { get; set; }
        public string ClasificacionVisual { get; set; }
        public string DescripcionCriticidad { get; set; }
        public int DiasEnArrastre { get; set; }
        public DateTime? FechaLimiteArrastre { get; set; }
        public int DiasRestantes { get; set; }
        public string EstadoTiempo { get; set; }
        public int OrdenPrioridad { get; set; }
        public int IdCarrera { get; set; }
        public int IdEspecialidad { get; set; }
        public int IdGrado { get; set; }
        public int IdGrupo { get; set; }
        public int IdTurno { get; set; }
        public int IdPeriodo { get; set; }

        public bool MateriaEstaActiva { get; set; }
        public string EstadoMateria { get; set; }

        public bool EstadoAlumno { get; set; }   // True: Activo, False: Baja
        public string EstadoMateriaAlumno { get; set; } // "Reprobada" o "Extraordinario"
    }

    // DTO PARA RESUMEN USANDO LA FUNCIÓN fn_ResumenArrastreCarrera
    public class ResumenArrastreCarreraDto
    {
        public int IdCarrera { get; set; }

        [Display(Name = "Total Alumnos con Arrastre")]
        public long TotalAlumnosConArrastre { get; set; }

        [Display(Name = "Total Materias en Arrastre")]
        public long TotalMateriasEnArrastre { get; set; }

        [Display(Name = "Total Especialidades Afectadas")]
        public long TotalEspecialidadesAfectadas { get; set; }

        [Display(Name = "Total Grupos Afectados")]
        public long TotalGruposAfectados { get; set; }

        // ⭐ CONTEOS POR NIVEL DE CRITICIDAD BASADO EN TIEMPO (ACTUALIZADO)
        [Display(Name = "Materias Fuera de Tiempo")]
        public long MateriasFueraDeTiempo { get; set; }

        [Display(Name = "Materias Críticas (≤60 días)")]
        public long MateriasCriticas { get; set; }

        [Display(Name = "Materias Medias (60-180 días)")]
        public long MateriasMedias { get; set; }

        [Display(Name = "Materias En Tiempo (>180 días)")]
        public long MateriasEnTiempo { get; set; }

        // ⭐ CONTEOS POR ESTADO DE TIEMPO (Los 4 estados específicos)
        [Display(Name = "Casos Fuera de Tiempo")]
        public long CasosFueraDeTiempo { get; set; }

        [Display(Name = "Casos Críticos")]
        public long CasosCriticos { get; set; }

        [Display(Name = "Casos Medios")]
        public long CasosMedios { get; set; }

        [Display(Name = "Casos En Tiempo")]
        public long CasosEnTiempo { get; set; }

        // PROMEDIOS Y ESTADÍSTICAS
        [Display(Name = "Promedio Intentos")]
        public double PromedioIntentos { get; set; }

        [Display(Name = "Promedio Días en Arrastre")]
        public double PromedioDiasEnArrastre { get; set; }

        // ANÁLISIS DE CRITICIDAD
        [Display(Name = "Alumnos en Riesgo Crítico")]
        public long AlumnosRiesgoCritico { get; set; }

        [Display(Name = "Casos Requieren Atención Inmediata")]
        public long CasosAtencionInmediata { get; set; }

        // ESPECIALIDAD MÁS AFECTADA
        [Display(Name = "Especialidad Más Afectada")]
        public string EspecialidadMasAfectada { get; set; }

        [Display(Name = "Casos en Esp. Más Afectada")]
        public long CasosEspecialidadMasAfectada { get; set; }
    }
    // CLASE AUXILIAR PARA DATOS RAW DE CONSULTA DE CARRERA (MANTENER PARA COMPATIBILIDAD)
    public class ArrastreCarreraRawDto
    {
        public int IdPersona { get; set; }
        public int IdMateria { get; set; }
        public string Matricula { get; set; }
        public string NombreAlumno { get; set; }
        public string NombreCarrera { get; set; }
        public string NombreEspecialidad { get; set; }
        public string NombreMateria { get; set; }
        public int IntentosExtraordinarios { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
        public string Observaciones { get; set; }
        public string NombreGrado { get; set; }
        public string NombreGrupo { get; set; }
        public string NombreTurno { get; set; }
        public string NombrePeriodo { get; set; }
        public int? CuatrimestreMateria { get; set; }
        public int Año { get; set; }
        public string TutorAsignado { get; set; }

        // CAMPOS PARA AGRUPACIÓN
        public int IdCarrera { get; set; }
        public int IdEspecialidad { get; set; }
        public int IdGrado { get; set; }
        public int IdGrupo { get; set; }
        public int IdTurno { get; set; }
        public int IdPeriodo { get; set; }
    }

    // CLASE PARA SELECTOR DE CARRERA EN EL DROPDOWN
    public class CarreraSeleccionDto
    {
        public int IdCarrera { get; set; }
        public string NombreCarrera { get; set; }
        public string Nomenclatura { get; set; }
        public int TotalEspecialidades { get; set; }
        public int TotalAlumnosConArrastre { get; set; }
        public string DisplayText { get; set; }
    }

    // CLASE PARA FILTROS AVANZADOS DE COORDINADOR
    public class FiltrosArrastreCoordinadorDto
    {
        public int? IdCarrera { get; set; }
        public int? IdEspecialidad { get; set; }
        public int? IdGrado { get; set; }
        public int? IdTurno { get; set; }
        public int? IdPeriodo { get; set; }
        public int? Año { get; set; }
        public string NivelCriticidad { get; set; } // "critico", "medio", "bajo"
        public string EstadoTiempo { get; set; } // "fuera", "critico", "riesgo", "tiempo"
        public bool SoloCriticos { get; set; }
        public bool SoloFueraDeTiempo { get; set; }
    }

    // CLASE PARA ESTADÍSTICAS POR ESPECIALIDAD
    public class EspecialidadEstadisticaDto
    {
        public int IdEspecialidad { get; set; }
        public string NombreEspecialidad { get; set; }
        public int TotalAlumnosConArrastre { get; set; }
        public int TotalMateriasEnArrastre { get; set; }
        public int MateriasCriticas { get; set; }
        public int MateriasMedias { get; set; }
        public int MateriasRecientes { get; set; }
        public decimal PorcentajeDelTotal { get; set; }
        public string ClasificacionEspecialidad { get; set; } // "Crítica", "Media", "Baja"
        public double PromedioIntentos { get; set; }
        public int CasosFueraDeTiempo { get; set; }
    }

    // CLASE PARA COMPATIBILIDAD CON LA VISTA EXISTENTE (si es necesaria)
    public class ArrastreCarreraVistaDto
    {
        public int IdPersona { get; set; }
        public int IdCarrera { get; set; }
        public int IdEspecialidad { get; set; }
        public int IdGrado { get; set; }
        public int IdGrupo { get; set; }
        public int IdTurno { get; set; }
        public int IdPeriodo { get; set; }
        public string Matricula { get; set; }
        public string NombreAlumno { get; set; }
        public string NombreCarrera { get; set; }
        public string NombreEspecialidad { get; set; }
        public string GradoGrupo { get; set; }
        public string MateriaArrastre { get; set; }
        public int CuatrimestreMateria { get; set; }
        public string CuatrimestreTexto { get; set; }
        public int IntentosExtraordinarios { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
        public string Observaciones { get; set; }
        public string NombreTurno { get; set; }
        public string NombrePeriodo { get; set; }
        public int Año { get; set; }
        public string TutorAsignado { get; set; }
        public int NivelCriticidad { get; set; }
        public string ClasificacionVisual { get; set; }
        public string DescripcionCriticidad { get; set; }
        public int DiasEnArrastre { get; set; }
        public DateTime? FechaLimiteArrastre { get; set; }
        public int DiasRestantes { get; set; }
        public string EstadoTiempo { get; set; }
        public int OrdenPrioridad { get; set; }
    }

    // CLASE PARA ANÁLISIS AVANZADO DE COORDINADOR
    public class AnalisisAvanzadoCarreraDto
    {
        public int IdCarrera { get; set; }
        public string NombreCarrera { get; set; }

        // Tendencias temporales
        public double TendenciaUltimoMes { get; set; }
        public double TendenciaUltimoTrimestre { get; set; }

        // Distribución por especialidades
        public List<EspecialidadEstadisticaDto> EspecialidadesRanking { get; set; }

        // Indicadores de rendimiento
        public double IndiceSaludAcademica { get; set; } // 0-100
        public string ClasificacionCarrera { get; set; } // "Excelente", "Buena", "Regular", "Crítica"

        // Recomendaciones automáticas
        public List<string> Recomendaciones { get; set; }
        public List<string> AlertasCriticas { get; set; }

        public AnalisisAvanzadoCarreraDto()
        {
            EspecialidadesRanking = new List<EspecialidadEstadisticaDto>();
            Recomendaciones = new List<string>();
            AlertasCriticas = new List<string>();
        }
    }

    // CLASE PARA PROYECCIONES Y PRONÓSTICOS
    public class ProyeccionArrastreDto
    {
        public int IdCarrera { get; set; }
        public DateTime FechaProyeccion { get; set; }

        // Proyecciones a corto plazo (1-3 meses)
        public int ProyeccionCortoPlazo { get; set; }
        public double ProbabilidadMejora { get; set; }

        // Proyecciones a mediano plazo (3-6 meses)
        public int ProyeccionMedianoPlazo { get; set; }
        public double ProbabilidadRecuperacion { get; set; }

        // Factores de riesgo identificados
        public List<string> FactoresRiesgo { get; set; }

        // Estrategias sugeridas
        public List<string> EstrategiasIntervencion { get; set; }

        public ProyeccionArrastreDto()
        {
            FactoresRiesgo = new List<string>();
            EstrategiasIntervencion = new List<string>();
        }
    }
}
