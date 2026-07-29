using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesExtras;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaWeb.Models.Materias
{
    public class ExportarMateriasGrupo
    {
        [Key]
        public int IdExportacion { get; set; }

        [Required]
        public int IdGrupo { get; set; }

        [Required]
        public int IdCarrera { get; set; }

        [Required]
        public int IdGrado { get; set; }

        public DateTime FechaExportacion { get; set; }

        public int TotalAlumnos { get; set; }

        public int TotalMaterias { get; set; }

        public string NombreArchivo { get; set; }

        public string UsuarioExportacion { get; set; }

        // Propiedades de navegación
        public virtual Grupo Grupo { get; set; }
        public virtual Carrera Carrera { get; set; }
        public virtual Grado Grado { get; set; }

        // Propiedades calculadas
        [NotMapped]
        public string NombreCompleto
        {
            get
            {
                return $"Exportación_{Grupo?.Nombre}_{Carrera?.Nombre}_{FechaExportacion:yyyyMMdd}";
            }
        }

        [NotMapped]
        public string FechaFormateada
        {
            get
            {
                return FechaExportacion.ToString("dd/MM/yyyy HH:mm:ss");
            }
        }
    }

    // MODELO PARA VISTA DE RESUMEN DE EXPORTACIÓN
    public class ResumenExportacionGrupo
    {
        public string NombreGrupo { get; set; }
        public string NombreCarrera { get; set; }
        public string NombreGrado { get; set; }
        public int TotalAlumnos { get; set; }
        public int TotalMaterias { get; set; }
        public List<AlumnoResumen> Alumnos { get; set; }
        public List<MateriaResumen> Materias { get; set; }
        public DateTime FechaGeneracion { get; set; }

        public ResumenExportacionGrupo()
        {
            Alumnos = new List<AlumnoResumen>();
            Materias = new List<MateriaResumen>();
            FechaGeneracion = DateTime.Now;
        }
    }

    public class AlumnoResumen
    {
        public int IdPersona { get; set; }
        public string Matricula { get; set; }
        public string Nombre { get; set; }
        public bool EstaActivo { get; set; }
        public int MateriasAcreditadas { get; set; }
        public int MateriasReprobadas { get; set; }
        public int MateriasExtraordinario { get; set; }
        public int MateriasPendientes { get; set; }
        public decimal PromedioGeneral { get; set; }
        public string EstadoAcademico { get; set; }

        [NotMapped]
        public string EstadoTexto
        {
            get
            {
                if (MateriasReprobadas >= 3 || MateriasExtraordinario >= 4)
                    return "DADO DE BAJA";
                else if (MateriasReprobadas == 2 || MateriasExtraordinario == 3)
                    return "EN RIESGO";
                else
                    return "ACTIVO";
            }
        }

        [NotMapped]
        public string EstadoLabel
        {
            get
            {
                switch (EstadoTexto)
                {
                    case "DADO DE BAJA": return "label-danger";
                    case "EN RIESGO": return "label-warning";
                    case "ACTIVO": return "label-success";
                    default: return "label-default";
                }
            }
        }
    }

    public class MateriaResumen
    {
        public int IdMateria { get; set; }
        public string Nombre { get; set; }
        public int AlumnosInscritos { get; set; }
        public int AlumnosAcreditados { get; set; }
        public int AlumnosReprobados { get; set; }
        public int AlumnosExtraordinario { get; set; }
        public int AlumnosPendientes { get; set; }
        public decimal PromedioMateria { get; set; }
        public decimal PorcentajeAprobacion { get; set; }

        [NotMapped]
        public string EstadisticaTexto
        {
            get
            {
                return $"{AlumnosAcreditados}/{AlumnosInscritos} aprobados ({PorcentajeAprobacion:0.0}%)";
            }
        }

        [NotMapped]
        public string EstadoMateria
        {
            get
            {
                if (PorcentajeAprobacion >= 80) return "EXCELENTE";
                else if (PorcentajeAprobacion >= 70) return "BUENO";
                else if (PorcentajeAprobacion >= 60) return "REGULAR";
                else return "CRÍTICO";
            }
        }

        [NotMapped]
        public string EstadoLabel
        {
            get
            {
                switch (EstadoMateria)
                {
                    case "EXCELENTE": return "label-success";
                    case "BUENO": return "label-info";
                    case "REGULAR": return "label-warning";
                    case "CRÍTICO": return "label-danger";
                    default: return "label-default";
                }
            }
        }
    }

    // MODELO PARA CONFIGURACIÓN DE EXPORTACIÓN

    public class ConfiguracionExportacion
    {
        public int IdGrupo { get; set; }
        public int IdCarrera { get; set; }
        public int IdGrado { get; set; }
        public bool IncluirEstadoAcademico { get; set; }
        public bool IncluirPromedios { get; set; }
        public bool IncluirFechas { get; set; }
        public bool IncluirObservaciones { get; set; }
        public bool SoloMateriasConCalificacion { get; set; }
        public bool FiltrarAlumnosActivos { get; set; }
        public string FormatoFecha { get; set; }
        public string TipoReporte { get; set; }

        public ConfiguracionExportacion()
        {
            IncluirEstadoAcademico = true;
            IncluirPromedios = true;
            IncluirFechas = false;
            IncluirObservaciones = false;
            SoloMateriasConCalificacion = false;
            FiltrarAlumnosActivos = true;
            FormatoFecha = "dd/MM/yyyy";
            TipoReporte = "COMPLETO";
        }
    }

    // MODELO PARA HISTORIAL DE EXPORTACIONES

    public class HistorialExportacion
    {
        [Key]
        public int IdHistorial { get; set; }

        public int IdGrupo { get; set; }
        public int IdCarrera { get; set; }
        public int IdGrado { get; set; }
        public DateTime FechaExportacion { get; set; }
        public string UsuarioExportacion { get; set; }
        public string NombreArchivo { get; set; }
        public int TotalAlumnos { get; set; }
        public int TotalMaterias { get; set; }
        public string TipoExportacion { get; set; }
        public bool ExportacionExitosa { get; set; }
        public string ObservacionesExportacion { get; set; }

        // Propiedades de navegación
        public virtual Grupo Grupo { get; set; }
        public virtual Carrera Carrera { get; set; }
        public virtual Grado Grado { get; set; }

        [NotMapped]
        public string DescripcionCompleta
        {
            get
            {
                return $"{Grupo?.Nombre} - {Carrera?.Nombre} - {Grado?.Nombre} ({FechaExportacion:dd/MM/yyyy})";
            }
        }

        [NotMapped]
        public string EstadoExportacion
        {
            get
            {
                return ExportacionExitosa ? "EXITOSA" : "CON ERRORES";
            }
        }

        [NotMapped]
        public string EstadoLabel
        {
            get
            {
                return ExportacionExitosa ? "label-success" : "label-danger";
            }
        }
    }
}