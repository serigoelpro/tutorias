using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class NivelDesempeño
    {
        [Key]
        public int IdNivelDesempeño { get; set; }

        [Required]
        [DisplayName("NivelDesempeño")]
        public string TipoNivelDesempeño { get; set; }

    }
}