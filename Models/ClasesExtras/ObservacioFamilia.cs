using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class ObservacioFamilia
    {
        [Key]
        public int IdObservacionFamilia { get; set; }

        [Required]
        [DisplayName("Nombre")]
        public string Nombre { get; set; }

    }
}