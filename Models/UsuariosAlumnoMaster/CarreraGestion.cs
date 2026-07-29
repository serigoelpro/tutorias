using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Plataforma_Web.Models.UsuariosAlumnoMaster
{
    [Table("Carreras")]
    public class CarreraGestion
    {
        [Key]
        public int IdCarrera { get; set; }

        [Display(Name = "Nombre")]
        public string Nombre { get; set; }

        [Display(Name = "Estado")]
        public bool? Estado { get; set; }
    }
}