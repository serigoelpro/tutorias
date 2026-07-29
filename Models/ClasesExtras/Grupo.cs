using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class Grupo
    {
        [Key]
        public int IdGrupo { get; set; }

        [DisplayName("grupo")]
        [Required]
        public string Nombre{ get; set; }
    }
}