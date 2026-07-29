using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace PlataformaWeb.Models
{
    public class Individual
    {

        [Key]
        public int IdIndividual { get; set; }

        //[DataType(DataType.Date)]
        //[DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime Fecha { get; set; }

        public int IdPersona { get; set; }

        public string Matricula { get; set; }

        [Required]
        [DisplayName("Área")]
        public string Especialidad { get; set; }

        [DisplayName("Área")]
        public string Area { get; set; }

        [DisplayName("Carrera")]
        public string Carrera { get; set; }//nombre y especialidad
       
        [DisplayName("Nombre completo")]
        public string Nombre { get; set; }
       
        [DisplayName("Grupo")]
        public string Grupo { get; set; }//nomenclatura

        [DisplayName("Cuatrimestre")]
        public string Cuatrimestre { get; set; }//periodo

    }
}