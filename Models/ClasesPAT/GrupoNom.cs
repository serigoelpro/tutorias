using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models.ClasesPAT
{
    public class GrupoNom
    {
        [Key]
        public int IdGrupoNom { get; set; }

        [DisplayName("Nombre")]
        [Required]
        public string Nombre { get; set; }
    }
}