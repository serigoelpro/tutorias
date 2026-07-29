using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class Vulnerabilidad
    {
        [Key]
        public int IdVulnerabilidad { get; set; }

        [Required]
        [DisplayName("Tipo de vulnerabilidad")]
        public string TipoVulnerabilidad { get; set; }
    }
}