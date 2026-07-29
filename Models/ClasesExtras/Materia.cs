using Plataforma_Web.Models.ClasesExtras;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Plataforma_Web.Models
{
    public class Materia
    {
        [Key] public int IdMateria { get; set; }

        [Required, StringLength(100)]
        public string Nombre { get; set; }

        public int IdCarrera { get; set; }
        public int IdGrado { get; set; }
        public bool Activo { get; set; } = true;

        public virtual Carrera Carrera { get; set; }
        public virtual Grado Grado { get; set; }
    }
}