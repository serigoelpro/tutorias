using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;


namespace PlataformaWeb.BecasTransporte.Models
{
    public class Beca
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DisplayName("Nombre de la beca")]
        public string NombreBeca { get; set; }

        [Required(ErrorMessage = "Ingrese una descripcion para el tipo de beca.")]
        [DisplayName("Descripción de la beca")]
        public string DetallesBeca { get; set; }


    }
}