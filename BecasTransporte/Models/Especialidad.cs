using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Plataforma_Web.Models;


namespace PlataformaWeb.BecasTransporte.Models
{
    public class Especialidad
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DisplayName("Carrera a la que pertenece")]
        public int IdCarrera { get; set; }
        public virtual Carrera Carrera { get; set; }

        [Required]
        [DisplayName("Nombre de la especialidad")]
        public string Nombre { get; set; }

        [DisplayName("Sistema Nuevo")]
        public bool? SistemaNuevo { get; set; }

    }
}