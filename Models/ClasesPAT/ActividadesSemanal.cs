using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models.ClasesPAT
{
    public class ActividadesSemanal
    {
        [Key]
        public int IdActividad { get; set; }

        [Required]
        [DisplayName("IdEntrevistaInicial")]
        public int IdEntrevistaInicial { get; set; }
        public virtual PAT PAT { get; set; }

        [Required]
        [DisplayName("Semana")]
        public int IdSemana { get; set; }
        public virtual Semana Semana { get; set; }

        [Required]
        [DisplayName("Tipo de tutoria")]
        public int IdTipoTutoria { get; set; }
        public virtual TipoTutoria Tipo { get; set; }

        [Required]
        [DisplayName("Actividad")]
        public string Actividad { get; set; }

        [Required]
        [DisplayName("¿Se cumplio la actividad?")]
        public bool RealizoActividad { get; set; }


        [DisplayName("Notas")]
        public string Comentarios { get; set; }

        [DisplayName("Firma de revision")]
        public bool Firma { get; set; }
    }
}