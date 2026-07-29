using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models.PrimeraEntrevista.Secundarios
{
    public class Respuesta10
    {
        [Key]
        public int IdVulnerable { get; set; }

        [DisplayName("Nombre")]
        [Required]
        public string Nombre { get; set; }
    }
}