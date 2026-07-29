using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models.PrimeraEntrevista.Secundarios
{
    public class TempGrupo
    {
        [Key]
        public int IdTemp { get; set; }

        public int IdUsuario { get; set; }

        [DisplayName("Nombre")]
        [Required]
        public int Grupo { get; set; }
    }
}