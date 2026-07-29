using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models.PrimeraEntrevista.Secundarios
{
    public class Respuesta14
    {
        [Key]
        public int IdTipoDispositivo { get; set; }

        [DisplayName("Nombre")]
        [Required]
        public string Nombre { get; set; }
    }
}