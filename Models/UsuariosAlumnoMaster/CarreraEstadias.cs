using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Plataforma_Web.Models.UsuariosAlumnoMaster
{
    [Table("Carrera")]
    public class CarreraEstadias
    {
        [Key]
        public int IdArea { get; set; }

        [Display(Name = "Área")]
        public string Area { get; set; }

        [Display(Name = "Carrera Alumno")]
        public string CarreraAlumno { get; set; }

        [Display(Name = "Es Maestría")]
        public bool EsMaestria { get; set; }
    }
}