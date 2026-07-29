using Microsoft.Reporting.WebForms;
using Plataforma_Web.Models;
using Plataforma_Web.Models.PrimeraEntrevista;
using Plataforma_Web.Models.PrimeraEntrevista.Secundarios;
using PlataformaWeb;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Plataforma_Web.Controllers.Asesorar
{
    [CustomAuthorize(Nivel = 2)]
    public class AsesorMasterController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();
        // GET: Asesor
        public ActionResult Index()
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                pa = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                pa = 2;
            }
            else
            {
                pa = 3;
            }
            List<TutoriaGrupal> tutorias = db.TutoriaGrupals.Where(x => x.IdUsuario == usuario.IdUsuario && x.IdPeriodo == pa && x.Año == DateTime.Now.Year).ToList();
            foreach (var item in tutorias)
            {
                var x = item.Carrera.Nombre.ToString() + ", " + item.Grado.Nombre.ToString() + item.Grupo.Nombre.ToString() + ", " + item.Turno.Nombre.ToString() + ", " + item.Periodo.Nombre.ToString() + ", " + item.Año.ToString();
                item.Nomenclatura = x.ToString();
            }

            ViewBag.Grupos = tutorias.Select(p => new SelectListItem() { Value = p.IdTutoriaGrupal.ToString(), Text = p.Nomenclatura }).ToList<SelectListItem>();
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(TutoriaGrupal tutoriaGrupal)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (tutoriaGrupal.IdTutoriaGrupal == 0)
            {
                ViewBag.Mensaje = "Seleccione un grupo por favor.";
            }
            else
            {
                return RedirectToAction("Grupo", new { id = tutoriaGrupal.IdTutoriaGrupal });
                //TempGrupo alum = new TempGrupo();
                //var alu = db.TempGrupos.FirstOrDefault(x => x.IdUsuario == usuario.IdUsuario);
                //if (alu == null)
                //{
                //    alum.IdUsuario = usuario.IdUsuario;
                //    alum.Grupo = tutoriaGrupal.IdTutoriaGrupal;

                //    db.TempGrupos.Add(alum);
                //    db.SaveChanges();
                //    return RedirectToAction("Grupo", new { id = tutoriaGrupal.IdTutoriaGrupal });
                //}
                //else
                //{
                //    alum = alu;
                //    alum.IdUsuario = usuario.IdUsuario;
                //    alum.Grupo = tutoriaGrupal.IdTutoriaGrupal;
                //    db.Entry(alum).State = EntityState.Modified;
                //    db.SaveChanges();
                //    return RedirectToAction("Grupo", new { id = tutoriaGrupal.IdTutoriaGrupal });
                //}


            }

            var tiempo = DateTime.Now;
            var pa = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                pa = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                pa = 2;
            }
            else
            {
                pa = 3;
            }
            List<TutoriaGrupal> tutorias = db.TutoriaGrupals.Where(x => x.IdUsuario == usuario.IdUsuario && x.IdPeriodo == pa && x.Año == DateTime.Now.Year).ToList();
            foreach (var item in tutorias)
            {
                var x = item.Carrera.Nombre.ToString() + ", " + item.Grado.Nombre.ToString() + item.Grupo.Nombre.ToString() + ", " + item.Turno.Nombre.ToString() + ", " + item.Periodo.Nombre.ToString() + ", " + item.Año.ToString();
                item.Nomenclatura = x.ToString();
            }

            ViewBag.Grupos = tutorias.Select(p => new SelectListItem() { Value = p.IdTutoriaGrupal.ToString(), Text = p.Nomenclatura }).ToList<SelectListItem>();
            return View();
        }
        public ActionResult Grupo(int? id)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (id == null || id == 0)
            {
                return RedirectToAction("Index");
            }



            var grupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == id);
            List<DatosPersonales> datosPersonales = db.DatosPersonales.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado &&
            x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno && x.Año == grupo.Año && x.IdPeriodo == grupo.IdPeriodo).OrderBy(x => x.Nombre).ToList();
            ViewBag.Alumnos = datosPersonales;
            return View();

            //var alu = db.TempGrupos.FirstOrDefault(x => x.IdUsuario == usuario.IdUsuario);
            //if(alu == null)
            //{
            //    var grupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == id);
            //    List<DatosPersonales> datosPersonales = db.DatosPersonales.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado && x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno).ToList();
            //    ViewBag.Alumnos = datosPersonales;
            //    return View();
            //}
            //else
            //{
            //    id = alu.Grupo;
            //    var grupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == id);
            //    List<DatosPersonales> datosPersonales = db.DatosPersonales.Where(x => x.IdCarrera == grupo.IdCarrera && x.IdGrado == grupo.IdGrado && x.IdGrupo == grupo.IdGrupo && x.IdTurno == grupo.IdTurno).ToList();
            //    ViewBag.Alumnos = datosPersonales;
            //    return View();
            //}

        }
        public ActionResult Error()
        {
            ViewBag.Mensaje1 = "Ocurrió un error al procesar los datos del alumno, pida al alumno completar la entrevista y vuelva a intentar";
            return View();
        }
        public ActionResult Detalles(int? id)
        {


            var comp = db.EntrevistaInicials.FirstOrDefault(x => x.IdPersona == id);
            var dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
            var aa = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
            var ae = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == id);
            var ap = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == id);
            if (ap == null || dp == null || aa == null || ae == null)
            {
                return View("Error");
            }
            Usuario usuario = Session["Usuario"] as Usuario;
            var tiempo = DateTime.Now;
            var turn = 0;
            if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
            {
                turn = 1;
            }
            else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
            {
                turn = 2;
            }
            else
            {
                turn = 3;
            }
            DatosPersonales ei = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
            TutoriaGrupal tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == ei.IdCarrera && x.IdGrado == ei.IdGrado && x.IdGrupo == ei.IdGrupo && x.IdTurno == ei.IdTurno && x.IdUsuario == usuario.IdUsuario && x.IdPeriodo == turn && x.Año == DateTime.Now.Year);

            ViewBag.id = tutgrup.IdTutoriaGrupal;
            if (comp == null)
            {
                ViewBag.datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
                ViewBag.academicos = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
                ViewBag.economicos = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == id);
                ViewBag.personales = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == id);
                ViewBag.IdVulnerable = new SelectList(db.Respuesta10.OrderByDescending(x => x.IdVulnerable), "IdVulnerable", "Nombre");
                ViewBag.IdEleccionVunerabilidad = new SelectList(db.Vulnerable.OrderByDescending(x => x.IdEleccionVunerabilidad), "IdEleccionVunerabilidad", "Nombre");
                return View();

            }
            else
            {
                ViewBag.datos = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
                ViewBag.academicos = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
                ViewBag.economicos = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == id);
                ViewBag.personales = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == id);
                ViewBag.IdVulnerable = new SelectList(db.Respuesta10.OrderByDescending(x => x.IdVulnerable), "IdVulnerable", "Nombre", comp.IdVulnerable);
                ViewBag.IdEleccionVunerabilidad = new SelectList(db.Vulnerable.OrderByDescending(x => x.IdEleccionVunerabilidad), "IdEleccionVunerabilidad", "Nombre", comp.IdEleccionVunerabilidad);
                return View(comp);
            }
        }
        [HttpPost]
        public ActionResult Detalles(EntrevistaInicial ei, int id)
        {
            var dp = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
            var aa = db.AspectosAcademicos.FirstOrDefault(x => x.IdPersona == id);
            var ae = db.AspectosEconomicos.FirstOrDefault(x => x.IdPersona == id);
            var ap = db.AspectosPersonales.FirstOrDefault(x => x.IdPersona == id);
            Usuario usuario = Session["Usuario"] as Usuario;
            var tiemp = DateTime.Now;
            var tur = 0;
            if (tiemp.Month == 1 || tiemp.Month == 2 || tiemp.Month == 3 || tiemp.Month == 4)
            {
                tur = 1;
            }
            else if (tiemp.Month == 5 || tiemp.Month == 6 || tiemp.Month == 7 || tiemp.Month == 8)
            {
                tur = 2;
            }
            else
            {
                tur = 3;
            }
            TutoriaGrupal tutgrupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == dp.IdCarrera && x.IdGrado == dp.IdGrado && x.IdGrupo == dp.IdGrupo && x.IdTurno == dp.IdTurno && x.IdUsuario == usuario.IdUsuario && x.IdPeriodo == tur && x.Año == DateTime.Now.Year);

            ViewBag.id = tutgrupo.IdTutoriaGrupal;
            var comp = db.EntrevistaInicials.FirstOrDefault(x => x.IdPersona == id);
            if (comp == null)
            {
                ei.IdPersona = id;
                ei.Fecha = dp.Fecha;
                ei.Matricula = dp.Matricula;
                ei.Nombre = dp.Nombre;
                ei.Edad = dp.Edad;
                ei.IdTurno = dp.IdTurno;
                ei.IdCarrera = dp.IdCarrera;
                ei.IdGrupo = dp.IdGrupo;
                ei.IdGrado = dp.IdGrado;
                ei.Direccion = dp.Direccion;
                ei.Celular = dp.Celular;
                ei.Telefono = dp.Telefono;
                ei.TelEmergencia = dp.TelEmergencia;
                ei.Email = dp.Email;
                ei.Foto = dp.Foto;
                ei.Sexo = dp.Sexo;
                ei.CarreraNom = dp.CarreraNom;
                ei.Area = dp.Area;


                //Aspectos Academicos 
                ei.IdListaBachillerato = aa.IdListaBachillerato;
                ei.Bachillerato = aa.Bachillerato;
                ei.Especialidad = aa.Especialidad;
                ei.Promedio = aa.Promedio;
                ei.MateriasDif = aa.MateriasDif;
                //ei.IdTecnicaEst = aa.IdTecnicaEst;
                //ei.TecnicaEst = aa.TecnicaEst;
                //ei.EquipoComp = aa.EquipoComp;

                //Aspectos Economicos
                ei.IdCiudad = ae.IdCiudad;
                ei.Ciudad = ae.Ciudad;
                ei.Familiar = ae.Familiar;
                ei.IdTrabajo = ae.IdTrabajo;
                ei.Trabaja = ae.Trabaja;
                //ei.IdDependientes = ae.IdDependientes;
                ei.Dependiente = ae.Dependiente;
                ei.OcupacionPapa = ae.OcupacionPapa;
                ei.OcupacionMama = ae.OcupacionMama;
                ei.CantidadHermano = ae.CantidadHermano;
                ei.IngresoM = ae.IngresoM;
                ei.CantidadPersonas = ae.CantidadPersonas;
                ei.CantidadTrabajan = ae.CantidadTrabajan;

                //Aspectos Personales
                ei.IdCasado = ap.IdCasado;
                ei.IdHijo = ap.IdHijo;
                ei.CantidadHijo = ap.CantidadHijo;
                ei.IdEnfermedad = ap.IdEnfermedad;
                ei.Especifica = ap.Especifica;
                ei.IdFuma = ap.IdFuma;
                ei.CantidadFuma = ap.CantidadFuma;
                ei.IdBebida = ap.IdBebida;
                ei.CantidadBedida = ap.CantidadBedida;
                ei.IdVidaSinSentido = ap.IdVidaSinSentido;
                ei.Porque = ap.Porque;
                ei.IdObservacionFamilia = ap.IdObservacionFamilia;
                ei.ApoyoFamiliaEnProblemas = ap.ApoyoFamiliaEnProblemas;
                ei.ApoyoFamiliaEnProblemasPorque = ap.ApoyoFamiliaEnProblemasPorque;
                ei.ProblemasEconomicosFamilia = ap.ProblemasEconomicosFamilia;
                ei.ProblemasEconomicosFamiliaPorque = ap.ProblemasEconomicosFamiliaPorque;
                ei.IdEmbarazo = ap.IdEmbarazo;

                db.EntrevistaInicials.Add(ei);
                db.SaveChanges();
                var alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
                alumno.Estado = true;
                db.Entry(alumno).State = EntityState.Modified;
                db.SaveChanges();


                var tiempo = DateTime.Now;
                var turn = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    turn = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    turn = 2;
                }
                else
                {
                    turn = 3;
                }
                TutoriaGrupal tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == ei.IdCarrera && x.IdGrado == ei.IdGrado && x.IdGrupo == ei.IdGrupo && x.IdTurno == ei.IdTurno && x.IdUsuario == usuario.IdUsuario && x.IdPeriodo == turn && x.Año == DateTime.Now.Year);
                id = tutgrup.IdTutoriaGrupal;


                return RedirectToAction("Grupo", new { id });
            }
            else
            {
                comp.IdPersona = id;
                comp.Fecha = dp.Fecha;
                comp.Matricula = dp.Matricula;
                comp.Nombre = dp.Nombre;
                comp.Edad = dp.Edad;
                comp.IdTurno = dp.IdTurno;
                comp.IdCarrera = dp.IdCarrera;
                comp.IdGrupo = dp.IdGrupo;
                comp.IdGrado = dp.IdGrado;
                comp.Direccion = dp.Direccion;
                comp.Celular = dp.Celular;
                comp.Telefono = dp.Telefono;
                comp.TelEmergencia = dp.TelEmergencia;
                comp.Email = dp.Email;
                comp.Foto = dp.Foto;
                comp.Sexo = dp.Sexo;
                comp.CarreraNom = dp.CarreraNom;
                comp.Area = dp.Area;

                //Aspectos Academicos 
                comp.IdListaBachillerato = aa.IdListaBachillerato;
                comp.Bachillerato = aa.Bachillerato;
                comp.Especialidad = aa.Especialidad;
                comp.Promedio = aa.Promedio;
                comp.MateriasDif = aa.MateriasDif;
                //comp.IdTecnicaEst = aa.IdTecnicaEst;
                //comp.TecnicaEst = aa.TecnicaEst;
                //comp.EquipoComp = aa.EquipoComp;
                //Aspectos Economicos
                comp.IdCiudad = ae.IdCiudad;
                comp.Ciudad = ae.Ciudad;
                comp.Familiar = ae.Familiar;
                comp.IdTrabajo = ae.IdTrabajo;
                comp.Trabaja = ae.Trabaja;
                //comp.IdDependientes = ae.IdDependientes;
                comp.Dependiente = ae.Dependiente;
                comp.OcupacionPapa = ae.OcupacionPapa;
                comp.OcupacionMama = ae.OcupacionMama;
                comp.CantidadHermano = ae.CantidadHermano;
                comp.IngresoM = ae.IngresoM;
                comp.CantidadPersonas = ae.CantidadPersonas;
                comp.CantidadTrabajan = ae.CantidadTrabajan;
                //Aspectos Personales
                comp.IdCasado = ap.IdCasado;
                comp.IdHijo = ap.IdHijo;
                comp.CantidadHijo = ap.CantidadHijo;
                comp.IdEnfermedad = ap.IdEnfermedad;
                comp.Especifica = ap.Especifica;
                comp.IdFuma = ap.IdFuma;
                comp.CantidadFuma = ap.CantidadFuma;
                comp.IdBebida = ap.IdBebida;
                comp.CantidadBedida = ap.CantidadBedida;
                comp.IdVidaSinSentido = ap.IdVidaSinSentido;
                comp.Porque = ap.Porque;
                comp.IdObservacionFamilia = ap.IdObservacionFamilia;
                comp.ApoyoFamiliaEnProblemas = ap.ApoyoFamiliaEnProblemas;
                comp.ApoyoFamiliaEnProblemasPorque = ap.ApoyoFamiliaEnProblemasPorque;
                comp.ProblemasEconomicosFamilia = ap.ProblemasEconomicosFamilia;
                comp.ProblemasEconomicosFamiliaPorque = ap.ProblemasEconomicosFamiliaPorque;
                comp.IdEmbarazo = ap.IdEmbarazo;
                //tutor
                comp.Area1 = ei.Area1;
                comp.NivelDesempeño1 = ei.NivelDesempeño1;
                comp.Area2 = ei.Area2;
                comp.NivelDesempeño2 = ei.NivelDesempeño2;
                comp.Area3 = ei.Area3;
                comp.NivelDesempeño3 = ei.NivelDesempeño3;
                comp.Area4 = ei.Area4;
                comp.NivelDesempeño4 = ei.NivelDesempeño4;
                comp.EvaluacionPsicometrica = ei.EvaluacionPsicometrica;
                comp.HabilidadesDeEstudio = ei.HabilidadesDeEstudio;
                comp.IdVulnerable = ei.IdVulnerable;
                comp.IdEleccionVunerabilidad = ei.IdEleccionVunerabilidad;
                DatosPersonales alumno = db.DatosPersonales.FirstOrDefault(x => x.IdPersona == id);
                alumno.Estado = true;

                db.Entry(comp).State = EntityState.Modified;
                db.Entry(alumno).State = EntityState.Modified;
                db.SaveChanges();




                var tiempo = DateTime.Now;
                var turn = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    turn = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    turn = 2;
                }
                else
                {
                    turn = 3;
                }
                TutoriaGrupal tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == comp.IdCarrera && x.IdGrado == comp.IdGrado && x.IdGrupo == comp.IdGrupo && x.IdTurno == comp.IdTurno && x.IdUsuario == usuario.IdUsuario && x.IdPeriodo == turn && x.Año == DateTime.Now.Year);
                id = tutgrup.IdTutoriaGrupal;


                return RedirectToAction("Grupo", new { id });
            }
        }
        public ActionResult ReporteEntrevista(int? id)
        {
            List<EntrevistaInicial> alumno = db.EntrevistaInicials.Where(x => x.IdPersona == id).ToList();
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                var tiempo = DateTime.Now;
                var turn = 0;
                if (tiempo.Month == 1 || tiempo.Month == 2 || tiempo.Month == 3 || tiempo.Month == 4)
                {
                    turn = 1;
                }
                else if (tiempo.Month == 5 || tiempo.Month == 6 || tiempo.Month == 7 || tiempo.Month == 8)
                {
                    turn = 2;
                }
                else
                {
                    turn = 3;
                }
                EntrevistaInicial ei = db.EntrevistaInicials.FirstOrDefault(x => x.IdPersona == id);
                TutoriaGrupal tutgrup = db.TutoriaGrupals.FirstOrDefault(x => x.IdCarrera == ei.IdCarrera && x.IdGrado == ei.IdGrado && x.IdGrupo == ei.IdGrupo && x.IdTurno == ei.IdTurno && x.IdUsuario == usuario.IdUsuario && x.IdPeriodo == turn && x.Año == DateTime.Now.Year);

                ViewBag.id = tutgrup.IdTutoriaGrupal;


                foreach (EntrevistaInicial v in alumno)
                {
                    string grupo = "";
                    var t = db.Turnoes.FirstOrDefault(x => x.IdTurno == v.IdTurno);
                    var c = db.Carreras.FirstOrDefault(x => x.IdCarrera == v.IdCarrera);
                    var grado = db.Gradoes.FirstOrDefault(x => x.IdGrado == v.IdGrado);
                    var grup = db.Grupoes.FirstOrDefault(x => x.IdGrupo == v.IdGrupo);

                    if (t.Nombre == "Matutino")
                    {
                        grupo += "M";
                    }
                    else if (t.Nombre == "Vespertino")
                    {
                        grupo += "I";
                    }
                    else if (t.Nombre == "Despresurizado")
                    {
                        grupo += "D";
                    }
                    grupo += c.Nomenclatura;
                    grupo += grado.Nombre;
                    grupo += grup.Nombre;

                    v.Grupo = grupo;

                    v.Ciudad = db.Respuesta1.FirstOrDefault(x => x.IdCiudad == v.IdCiudad).Nombre;
                    v.PTrabajas = db.Respuesta2.FirstOrDefault(x => x.IdTrabajo == v.IdTrabajo).Nombre;
                    //v.TDependientes = db.Respuesta3.FirstOrDefault(x => x.IdDependientes == v.IdDependientes).Nombre;
                    //v.TecnicaEstSiNo = db.Respuesta0.FirstOrDefault(x => x.IdTecnicaEst == v.IdTecnicaEst).Nombre;
                    v.Casado = db.Respuesta4.FirstOrDefault(x => x.IdCasado == v.IdCasado).Nombre;
                    v.Hijos = db.Respuesta5.FirstOrDefault(x => x.IdHijo == v.IdHijo).Nombre;
                    v.Enfermedad = db.Respuesta6.FirstOrDefault(x => x.IdEnfermedad == v.IdEnfermedad).Nombre;
                    v.Fuma = db.Respuesta7.FirstOrDefault(x => x.IdFuma == v.IdFuma).Nombre;
                    v.Bebida = db.Respuesta8.FirstOrDefault(x => x.IdBebida == v.IdBebida).Nombre;
                    v.VidaSinSentido = db.Respuesta9.FirstOrDefault(x => x.IdVidaSinSentido == v.IdVidaSinSentido).Nombre;
                    v.Observaciones = db.ObservacioFamilias.FirstOrDefault(x => x.IdObservacionFamilia == v.IdObservacionFamilia).Nombre;
                    v.Vulnerable = db.Respuesta10.FirstOrDefault(x => x.IdVulnerable == v.IdVulnerable).Nombre;
                    v.Embarazo = db.Respuesta11.FirstOrDefault(x => x.IdEmbarazo == v.IdEmbarazo).Nombre;
                    v.EleccionVulnerabilidad = db.Vulnerable.FirstOrDefault(x => x.IdEleccionVunerabilidad == v.IdEleccionVunerabilidad).Nombre;
                    v.ListaBachillerato = db.Respuesta12.FirstOrDefault(x => x.IdListaBachillerato == v.IdListaBachillerato).Nombre;


                }

                ReportViewer report1 = new ReportViewer();//Objeto de report viewer
                ReportDataSource rds = new ReportDataSource();//origen de datos
                rds.Value = alumno;//asigna la consulta de ventas como origen de datos
                rds.Name = "EntrevistaInicial";//Este nombre debe coincidir con el del informe
                report1.LocalReport.EnableExternalImages = true;
                report1.LocalReport.DataSources.Add(rds);//asignamos el origen de datos
                                                         //report1.LocalReport.ReportPath = Server.MapPath("~/Reporte/rptEntrevistaInicial.rdlc");
                report1.LocalReport.ReportPath = Server.MapPath("~/Reporte/EntrevistaInicialAlumno.rdlc");



                //Crea un parametro que almacene la foto en el formulario 
                //ReportParameter par = new ReportParameter("urlfoto", "file://" + Server.MapPath(solicitud[0].Foto));
                //report1.ProcessingMode = ProcessingMode.Local;
                //report1.LocalReport.SetParameters(par);
                ViewBag.ReportViewer = report1;//pasamos el objeto de reporte a la vista

            }

            catch (Exception ex)
            {
                ViewBag.Mensaje = ex.Message;
            }

            return View();
        }
    }
}