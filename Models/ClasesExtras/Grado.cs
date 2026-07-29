using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models.ClasesExtras
{
    public class Grado
    {
        [Key]
        public int IdGrado { get; set; }

        [DisplayName("Grado")]
        [Required]
        public string Nombre { get; set; }
    }
}