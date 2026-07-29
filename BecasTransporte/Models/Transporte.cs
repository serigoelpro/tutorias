using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PlataformaWeb.BecasTransporte.Models
{
    public class Transporte
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DisplayName("Ruta de transporte:")]
        public string Ruta { get; set; }
    }
}