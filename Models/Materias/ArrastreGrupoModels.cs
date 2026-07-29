using System;
using System.ComponentModel.DataAnnotations;

namespace PlataformaWeb.Models.Materias
{

    // CLASE DTO PARA MATERIAS DE ARRASTRE POR GRUPO

    public class ArrastreGrupoDto
    {
        public int IdPersona { get; set; }
        public int IdMateria { get; set; }
        public string Matricula { get; set; }
        public string NombreAlumno { get; set; }
        public string GradoGrupo { get; set; }
        public string MateriaArrastre { get; set; }

        // ✅ NUEVO: TIPO DE PROBLEMA
        public string TipoProblema { get; set; }  // "Arrastre" o "Extraordinario"
        public bool EsExtraordinario { get; set; }

        // ✅ NUEVO: INFORMACIÓN ESPECÍFICA PARA EXTRAORDINARIO
        public string PeriodoExtraordinario { get; set; }  // "Cuatrimestre en curso para presentar examen"
        public DateTime? FechaExamenExtraordinario { get; set; }

        // ✅ INFORMACIÓN DEL CUATRIMESTRE
        public int CuatrimestreMateria { get; set; }
        public string CuatrimestreTexto { get; set; }

        public int IntentosExtraordinarios { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
        public string Observaciones { get; set; }

        // ✅ ESTADO DE LA MATERIA
        public bool MateriaEstaActiva { get; set; }
        public string EstadoMateria { get; set; }

        // ✅ INFORMACIÓN DE CRITICIDAD SEGÚN CUATRIMESTRE
        public int NivelCriticidad { get; set; }
        public string ClasificacionVisual { get; set; }
        public string DescripcionCriticidad { get; set; }

        // ✅ INFORMACIÓN DE TIEMPO (8 MESES LÍMITE)
        public int DiasEnArrastre { get; set; }
        public DateTime? FechaLimiteArrastre { get; set; }
        public int DiasRestantes { get; set; }
        public string EstadoTiempo { get; set; }
        public int OrdenPrioridad { get; set; }
    }

    // CLASE DTO PARA RESUMEN DE ARRASTRE POR GRUPO
    public class ResumenArrastreDto
    {
        public int TotalAlumnosConArrastre { get; set; }
        public int TotalMateriasEnArrastre { get; set; }

        // ✅ POR ESTADO DE MATERIA
        public int MateriasActivasEnArrastre { get; set; }
        public int MateriasDesactivadasEnArrastre { get; set; }

        // ✅ POR CUATRIMESTRE (CRITICIDAD)
        public int MateriasCriticas_1er { get; set; }      // 1er cuatrimestre
        public int MateriasAltas_2do { get; set; }         // 2do cuatrimestre  
        public int MateriasMedias_3er { get; set; }        // 3er cuatrimestre
        public int MateriasRecientes_4to_mas { get; set; } // 4to+ cuatrimestres

        // ✅ NUEVO: SEPARACIÓN POR TIPO
        public int TotalMateriasArrastre { get; set; }
        public int TotalMateriasExtraordinario { get; set; }

        // ✅ POR TIEMPO (8 MESES LÍMITE)
        public int MateriasFueraDeTiempo { get; set; }
        public int MateriasCriticasTiempo { get; set; }
        public int MateriasEnRiesgo { get; set; }

        // ✅ PROMEDIOS
        public double PromedioIntentos { get; set; }
        public double PromedioDiasEnArrastre { get; set; }
    }

    // CLASE AUXILIAR PARA DATOS RAW DE CONSULTA
    public class ArrastreRawDto
    {
        public int IdPersona { get; set; }
        public int IdMateria { get; set; }
        public string Matricula { get; set; }
        public string NombreAlumno { get; set; }
        public string NombreMateria { get; set; }
        public int IntentosExtraordinarios { get; set; }
        public DateTime? FechaInicioArrastre { get; set; }
        public string Observaciones { get; set; }
        public string NombreGrado { get; set; }
        public string NombreGrupo { get; set; }
        public int? CuatrimestreMateria { get; set; }

        public DateTime? FechaExamenExtraordinario { get; set; }

        public string Estado { get; set; }

        public bool MateriaEstaActiva { get; set; }
        public string EstadoMateria { get; set; }
    }
}