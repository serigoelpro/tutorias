using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace Plataforma_Web.Models
{
    public class AlumnoMateria
    {
        [Key] public int Id { get; set; }
        public int IdAlumno { get; set; }
        public int IdMateria { get; set; }

        public virtual Alumno Alumno { get; set; }
        public virtual Materia Materia { get; set; }
    }
}