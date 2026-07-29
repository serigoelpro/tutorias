using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PlataformaWeb.Models.ClasesPAT
{
    public class AuxiliarPAT
    {
        [Key]
        public int IdAuxiliar { get; set; }
        public DateTime Fecha { get; set; }
        public string Cuatrimestre { get; set; }
        public string Grupo { get; set; }
        public string Carreras { get; set; }
        public string Tutor { get; set; }
        public int CantidadAlumno { get; set; }
        public int VunerableEconomico { get; set; }
        public string DescripcionEconomico { get; set; }
        public int VunerablePersonal { get; set; }
        public string DescripcionPersonal { get; set; }
        public int VunerableAcademico { get; set; }
        public string DescripcionAcademico { get; set; }
        public string Semana1 { get; set; }
        public string Semana2 { get; set; }
        public string Semana3 { get; set; }
        public string Semana4 { get; set; }
        public string Semana5 { get; set; }
        public string Semana6 { get; set; }
        public string Semana7 { get; set; }
        public string Semana8 { get; set; }
        public string Semana9 { get; set; }
        public string Semana10 { get; set; }
        public string Semana11 { get; set; }
        public string Semana12 { get; set; }
        public string Semana13 { get; set; }
        public string Semana14 { get; set; }
        public string Semana15 { get; set; }
        public string Semana16 { get; set; }
        public string TipoTutoria1 { get; set; }
        public string TipoTutoria2 { get; set; }
        public string Actividad { get; set; }
        public string RealizoActividad { get; set; }
        public string Comentarios { get; set; }

    }
}