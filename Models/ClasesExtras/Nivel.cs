using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class Nivel
    {
        [Key]
        public int IdNivel { get; set; }

        [DisplayName("Nivel de acceso")]
        [Required]
        public string Descripcion { get; set; }

    }
}