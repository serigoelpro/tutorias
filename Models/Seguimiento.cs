using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PlataformaWeb.Models
{
    public class Seguimiento
    {
        [Key]
        public int IdSeguimiento { get; set; }

        public int IdIndividual { get; set; }

        //[DataType(DataType.Date)]a
        //[DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime Fecha { get; set; }

        [Required]
        [DisplayName("Tipo de Vulnerabilidad")]
        public string Vulnerabilidad { get; set; }

        [Required]
        [DisplayName("Acción/Problematica")]
        public string Problematica { get; set; }

        [Required]
        [DisplayName("Acciones y/o Canalizaciones")]
        public string Accion { get; set; }

    }
}