using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesPAT;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PlataformaWeb.Models.ClasesPAT
{
    public class ActividadSemanalAux
    {
        [Key]
        public int IdActividad { get; set; }
        
        [Required]
        [DisplayName("Semana")]
        public int IdSemana { get; set; }

        [Required]
        [DisplayName("Actividad")]
        public string Actividad1 { get; set; }

        [Required]
        [DisplayName("Actividad")]
        public string Actividad2 { get; set; }


    }
}