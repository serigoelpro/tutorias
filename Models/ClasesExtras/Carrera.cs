using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class Carrera
    {
        [Key]

        public int IdCarrera { get; set; }

        [Required]
        [DisplayName("Carrera")]
        public string Nombre {get;set;}
        public string Nomenclatura { get; set; }
    }
}