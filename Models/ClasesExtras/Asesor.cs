using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models.ClasesExtras
{
    public class Asesor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DisplayName("Maestro")]
        public int IdUsuario { get; set; }
        public virtual Usuario Usuario { get; set; }
       
        [Required]
        [DisplayName("Telefono")]
        public string Telefono { get; set; }

        [Required]
        [DisplayName("Celular")]
        public string Celular { get; set; }

        [Required]
        [DisplayName("Correo")]
        public string Correo { get; set; }
    }
}