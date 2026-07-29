using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models.ClasesExtras
{
    public class RespuestaRubrica
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DisplayName("Respuesta")]
        public string Respuesta { get; set; }
    }
}