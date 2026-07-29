using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;


namespace Plataforma_Web.Models
{
    public class ResultadoExani
    {
        [Key]
        public int IdResultadoExani { get; set; }

        [Required]
        [DisplayName("ResultadoExani")]
        public string TipoResultadoExani { get; set; }
    }
}