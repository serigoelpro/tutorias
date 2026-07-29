using Microsoft.Reporting.WebForms;
using OfficeOpenXml.FormulaParsing.Excel.Functions.DateTime;
using Plataforma_Web.Models;
using Plataforma_Web.Models.ClasesExtras;
using Plataforma_Web.Models.ClasesPAT;
using Plataforma_Web.Models.PrimeraEntrevista;
using PlataformaWeb;
using PlataformaWeb.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace Plataforma_Web.Controllers.Asesorar
{
    [CustomAuthorize(Nivel = 3)]
    public class AsignarAsesoresController : Controller
    {
        private ModeloPlataforma db = new ModeloPlataforma();

        // GET: AsignarAsesores
        public ActionResult Index()
        {
            Usuario user = Session["Usuario"] as Usuario;
            if (user == null)
            {
                // Redirigir al login o manejar el error apropiadamente
                return RedirectToAction("Login", "Account"); // O la acción de login que uses
            }
            List<Usuario> us = new List<Usuario>();
            // ... resto del código ...

            foreach (Usuario usu in db.Usuarios.Where(x => x.IdNivel == 2 && x.IdCarrera == user.IdCarrera).ToList())
            {

                us.Add(usu);
            }
            return View(us);
        }

        public ActionResult Crear()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Crear(Usuario usuario)
        {
            if (usuario.NombreCompleto == null || usuario.NombreCompleto == "" || usuario.UserName == null || usuario.UserName == "" || usuario.Password == null || usuario.Password == "")
            {
                ViewBag.Mensaje = "Favor de llenar todos los campos.";
                return View(usuario);
            }
            if (ModelState.IsValid)
            {
                var users = db.Usuarios.FirstOrDefault(x => x.UserName == usuario.UserName && x.IdNivel == 2);
                var users2 = db.Usuarios.FirstOrDefault(x => x.NombreCompleto == usuario.NombreCompleto && x.IdNivel == 2);

                if (users == null && users2 == null)
                {
                    Usuario user = Session["Usuario"] as Usuario;
                    usuario.IdCarrera = user.IdCarrera;
                    usuario.IdNivel = 2;
                    usuario.Estado = true;
                    usuario.Tiempo = DateTime.Now;
                    usuario.Password = Security.Encripta(usuario.Password);
                    db.Usuarios.Add(usuario);
                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
                else
                {
                    ViewBag.Mensaje = "El nombre completo o de usuario ya esta registrado. Favor de intentar con otro.";
                    return View(usuario);
                }
            }
            return View();
        }

        public ActionResult EditarAsesor(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Usuario usuario = db.Usuarios.Find(id);
            usuario.Password = Security.Desencripta(usuario.Password);
            if (usuario == null)
            {
                return HttpNotFound();
            }
            return View(usuario);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditarAsesor(Usuario usuario)
        {
            if (usuario.NombreCompleto == null || usuario.NombreCompleto == "" || usuario.UserName == null || usuario.UserName == "" || usuario.Password == null || usuario.Password == "")
            {
                ViewBag.Mensaje = "Favor de llenar todos los campos.";
                return View(usuario);
            }
            if (ModelState.IsValid)
            {
                var users = db.Usuarios.FirstOrDefault(x => x.UserName == usuario.UserName &&
                x.IdUsuario != usuario.IdUsuario && x.IdNivel == usuario.IdNivel);
                var users2 = db.Usuarios.FirstOrDefault(x => x.NombreCompleto == usuario.NombreCompleto &&
                x.IdUsuario != usuario.IdUsuario && x.IdNivel == usuario.IdNivel);
                if (users == null && users2 == null)
                {
                    Usuario user = Session["Usuario"] as Usuario;
                    usuario.IdCarrera = user.IdCarrera;
                    usuario.IdNivel = 2;
                    usuario.Estado = true;
                    usuario.Tiempo = DateTime.Now;

                    usuario.Password = Security.Encripta(usuario.Password);
                    db.Entry(usuario).State = EntityState.Modified;
                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
                else
                {
                    ViewBag.Mensaje = "El nombre completo o de usuario ya esta registrado. Favor de intentar con otro.";
                    return View(usuario);
                }
            }
            return View(usuario);
        }

        public ActionResult EliminarAsesor(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            List<Usuario> us = new List<Usuario>();

            Usuario asesor = db.Usuarios.Find(id);
            if (asesor == null)
            {
                return HttpNotFound();
            }
            asesor.Password = Security.Desencripta(asesor.Password);
            return View(asesor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarAsesor(int id)
        {
            Usuario asesor = db.Usuarios.Find(id);
            if (asesor != null)
            {
                // Buscar y eliminar Tutorias y PATs asociados ANTES de eliminar al asesor
                var tutoriasAsociadas = db.TutoriaGrupals.Where(tg => tg.IdUsuario == id).ToList();
                foreach (var tutoria in tutoriasAsociadas)
                {
                    var patsAsociados = db.PATs.Where(p => p.IdTutoriaGrupal == tutoria.IdTutoriaGrupal).ToList();
                    if (patsAsociados.Any())
                    {
                        db.PATs.RemoveRange(patsAsociados);
                    }
                    db.TutoriaGrupals.Remove(tutoria);
                }
                // Ahora sí, eliminar al asesor
                db.Usuarios.Remove(asesor);
                db.SaveChanges(); // Guardar todos los cambios
            }
            return RedirectToAction("Index");
        }

        public ActionResult Asesorados(int? id)
        {
            ViewBag.Usuario = db.Usuarios.FirstOrDefault(x => x.IdUsuario == id);
            return View(db.TutoriaGrupals.Where(x => x.IdUsuario == id).ToList());
        }

        public ActionResult Asignar(int? id)
        {
            ViewBag.Usuario = db.Usuarios.FirstOrDefault(x => x.IdUsuario == id);
            ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre");
            ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre");
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
            ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre");
            return View();
        }

        [HttpPost]
        public ActionResult Asignar(TutoriaGrupal tutoriaGrupal, int id)
        {
            tutoriaGrupal.Año = DateTime.Now.Year;
            if (ModelState.IsValid)
            {
                Usuario user = Session["Usuario"] as Usuario;
                var tiempo = DateTime.Now;
                if (tiempo.Month >= 1 && tiempo.Month <= 4)
                {
                    tutoriaGrupal.IdPeriodo = 1;
                }
                else if (tiempo.Month >= 5 && tiempo.Month <= 8)
                {
                    tutoriaGrupal.IdPeriodo = 2;
                }
                else
                {
                    tutoriaGrupal.IdPeriodo = 3;
                }
                tutoriaGrupal.IdUsuario = id;

                var grupo = db.TutoriaGrupals.FirstOrDefault(x => x.IdGrado == tutoriaGrupal.IdGrado && x.IdGrupo == tutoriaGrupal.IdGrupo && x.IdCarrera == tutoriaGrupal.IdCarrera && x.IdTurno == tutoriaGrupal.IdTurno && x.IdPeriodo == tutoriaGrupal.IdPeriodo && x.Año == tutoriaGrupal.Año);
                if (grupo == null)
                {
                    db.TutoriaGrupals.Add(tutoriaGrupal);
                    db.SaveChanges();
                }
                else
                {
                    ViewBag.Usuario = db.Usuarios.FirstOrDefault(x => x.IdUsuario == id);
                    ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre");
                    ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre");
                    ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
                    ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre");
                    ViewBag.Mensaje = "El grupo que intenta crear ya esta asignado a un tutor.";
                    return View(tutoriaGrupal);
                }

                return RedirectToAction("Asesorados", new { id = tutoriaGrupal.IdUsuario });
            }
            ViewBag.Usuario = db.Usuarios.FirstOrDefault(x => x.IdUsuario == id);
            ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre");
            ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre");
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre");
            ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre");
            return View(tutoriaGrupal);
        }

        public ActionResult Eliminar(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TutoriaGrupal tutoriaGrupal = db.TutoriaGrupals.Find(id);
            if (tutoriaGrupal == null)
            {
                return HttpNotFound();
            }
            return View(tutoriaGrupal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Eliminar(int id)
        {
            TutoriaGrupal tutoriaGrupal = db.TutoriaGrupals.Find(id);

            List<PAT> pat = db.PATs.Where(x => x.IdTutoriaGrupal == id).ToList();
            db.TutoriaGrupals.Remove(tutoriaGrupal);
            db.SaveChanges();
            if (pat != null)
            {
                foreach (PAT p in pat)
                {
                    db.PATs.Remove(p);
                    db.SaveChanges();
                }
            }
            return RedirectToAction("Index");
        }

        public ActionResult Editar(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            TutoriaGrupal tutoriaGrupal = db.TutoriaGrupals.Find(id);
            if (tutoriaGrupal == null)
            {
                return HttpNotFound();
            }
            ViewBag.Usuario = db.Usuarios.FirstOrDefault(x => x.IdUsuario == id);
            ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre", tutoriaGrupal.IdGrado);
            ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre", tutoriaGrupal.IdGrupo);
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre", tutoriaGrupal.IdCarrera);
            ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre", tutoriaGrupal.IdTurno);
            return View(tutoriaGrupal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(TutoriaGrupal tutoriaGrupal)
        {
            if (ModelState.IsValid)
            {
                db.Entry(tutoriaGrupal).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.IdGrado = new SelectList(db.Gradoes, "IdGrado", "Nombre", tutoriaGrupal.IdGrado);
            ViewBag.IdGrupo = new SelectList(db.Grupoes, "IdGrupo", "Nombre", tutoriaGrupal.IdGrupo);
            ViewBag.IdCarrera = new SelectList(db.Carreras, "IdCarrera", "Nombre", tutoriaGrupal.IdCarrera);
            ViewBag.IdTurno = new SelectList(db.Turnoes, "IdTurno", "Nombre", tutoriaGrupal.IdTurno);
            return View(tutoriaGrupal);
        }

        public ActionResult PAT()
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            ViewBag.IdNivel = usuario.IdNivel;

            var tiempo = DateTime.Now;
            int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 :
                               (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
            int añoActual = tiempo.Year;

            // ============================================
            // PASO 1: Obtener TODAS las tutorías grupales según el nivel del usuario
            // ============================================
            List<TutoriaGrupal> todasLasTutorias;

            if (usuario.IdNivel == 4) // Master: todas las tutorías
            {
                todasLasTutorias = db.TutoriaGrupals
                    .Where(tg => tg.IdPeriodo == periodoActual && tg.Año == añoActual)
                    .ToList();
            }
            else // Coordinador: solo tutorías de su carrera
            {
                todasLasTutorias = db.TutoriaGrupals
                    .Where(tg => tg.IdCarrera == usuario.IdCarrera &&
                                 tg.IdPeriodo == periodoActual &&
                                 tg.Año == añoActual)
                    .ToList();
            }

            System.Diagnostics.Debug.WriteLine($"[PAT] Total tutorías encontradas: {todasLasTutorias.Count}");

            // ============================================
            // PASO 2: Verificar qué tutorías tienen PAT y cuáles no
            // ============================================
            var idsTutoriasConPAT = db.PATs
                .Where(p => p.IdPeriodo == periodoActual && p.Fecha.Year == añoActual)
                .Select(p => p.IdTutoriaGrupal)
                .ToList();

            var tutoriasSinPAT = todasLasTutorias
                .Where(t => !idsTutoriasConPAT.Contains(t.IdTutoriaGrupal))
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[PAT] Tutorías sin PAT: {tutoriasSinPAT.Count}");

            // ============================================
            // PASO 3: Crear PATs para tutorías que no tienen uno
            // ============================================
            bool esDirectorSoloLectura = usuario != null && usuario.EsDirector;
            int patsCreados = 0;
            if (!esDirectorSoloLectura)
            {
                foreach (var tutoria in tutoriasSinPAT)
                {
                    try
                    {
                        var tutor = db.Usuarios.FirstOrDefault(u => u.IdUsuario == tutoria.IdUsuario);
                        if (tutor != null)
                        {
                            var nuevoPAT = CrearPATParaTutoriaCoordinador(tutoria, tutor, periodoActual);
                            db.PATs.Add(nuevoPAT);
                            patsCreados++;

                            System.Diagnostics.Debug.WriteLine($"[PAT] PAT creado para grupo {GenerarNombreGrupoParaDiagnostico(tutoria)}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[PAT] ADVERTENCIA: Tutoría {tutoria.IdTutoriaGrupal} sin tutor asignado");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PAT] ERROR al crear PAT para tutoría {tutoria.IdTutoriaGrupal}: {ex.Message}");
                    }
                }

                if (patsCreados > 0)
                {
                    db.SaveChanges();
                    System.Diagnostics.Debug.WriteLine($"[PAT] {patsCreados} PATs creados exitosamente");
                }
            }

            // ============================================
            // PASO 4: Obtener TODOS los PATs (incluyendo históricos) para que los
            // filtros de Período/Año en la vista puedan mostrar registros de
            // cuatrimestres y años anteriores. La vista aplica por defecto el
            // período/año actual mediante applyDefaultFilters(), por lo que la
            // experiencia inicial no cambia para el usuario.
            // ============================================
            List<PAT> solicitud;

            if (usuario.IdNivel == 4) // Master: TODOS los PATs (todos los periodos/años)
            {
                solicitud = db.PATs.ToList();
            }
            else // Coordinador: TODOS los PATs de su carrera (todos los periodos/años)
            {
                solicitud = db.PATs
                    .Where(p => p.IdCarrera == usuario.IdCarrera)
                    .ToList();
            }

            // Cargar TODAS las tutorías referenciadas por los PATs (incluye históricas).
            // 'todasLasTutorias' del PASO 1 solo contiene las del periodo actual, por lo
            // que necesitamos un diccionario adicional para poder resolver lookups de
            // tutorías de períodos anteriores.
            var idsTutoriasReferenciadas = solicitud.Select(s => s.IdTutoriaGrupal).Distinct().ToList();
            var tutoriasLookup = db.TutoriaGrupals
                .Where(t => idsTutoriasReferenciadas.Contains(t.IdTutoriaGrupal))
                .ToDictionary(t => t.IdTutoriaGrupal);

            System.Diagnostics.Debug.WriteLine($"[PAT] Total PATs a mostrar (todos los periodos): {solicitud.Count}");
            // Cargar catálogos una sola vez
            // ... (Código anterior del método PAT) ...

            // ============================================
            // PASO 5: Procesar cada PAT (generar nomenclatura y datos)
            // Cargar catálogos y datos relacionados una sola vez ANTES del bucle
            // ============================================
            var todosLosTurnos = db.Turnoes.ToDictionary(t => t.IdTurno, t => t.Nombre);
            var todasLasCarreras = db.Carreras.ToDictionary(c => c.IdCarrera, c => new { c.Nombre, c.Nomenclatura });
            var todosLosGrados = db.Gradoes.ToDictionary(g => g.IdGrado, g => g.Nombre);
            var todosLosGruposLetra = db.Grupoes.ToDictionary(g => g.IdGrupo, g => g.Nombre);
            // La variable 'todasLasTutorias' ya fue cargada en el PASO 1, ¡no la redeclares aquí!
            // Simplemente la usamos para buscar eficientemente dentro del bucle.
            var idsTutores = solicitud.Select(s => s.IdTutor).Distinct().ToList();

            // CORRECCIÓN: Usamos directamente NombreCompleto y lo convertimos a mayúsculas
            var todosLosTutores = db.Usuarios
                                     .Where(u => idsTutores.Contains(u.IdUsuario))
                                     .ToDictionary(u => u.IdUsuario, u => (u.NombreCompleto ?? "").ToUpper());
            // --- INICIO NUEVO CÓDIGO ---
            // Crear un diccionario para almacenar los nombres de turno por PAT ID
            var turnosPorPatId = new Dictionary<int, string>();
            // --- FIN NUEVO CÓDIGO ---

            // Precomputar conteo histórico de alumnos por (Grupo|Cuatrimestre|Año) en una
            // sola consulta a Individuals. Evita N+1 queries dentro del foreach.
            var clavesHist = new Dictionary<int, string>(); // IdPAT -> "TI6C|Septiembre - Diciembre|2025"
            foreach (var p in solicitud)
            {
                if (!tutoriasLookup.TryGetValue(p.IdTutoriaGrupal, out var tHist)) continue;
                if (!EsPatHistorico(tHist)) continue;

                var carInfo = todasLasCarreras.ContainsKey(tHist.IdCarrera) ? todasLasCarreras[tHist.IdCarrera] : null;
                string nomCar = carInfo?.Nomenclatura ?? "";
                string nomGr = todosLosGrados.ContainsKey(tHist.IdGrado) ? todosLosGrados[tHist.IdGrado] : "";
                string nomLet = todosLosGruposLetra.ContainsKey(tHist.IdGrupo) ? todosLosGruposLetra[tHist.IdGrupo] : "";
                string grupoNomP = nomCar + nomGr + nomLet;
                string cuatriP = BuildCuatrimestreString(tHist.IdPeriodo);
                clavesHist[p.IdEntrevistaInicial] = grupoNomP + "|" + cuatriP + "|" + tHist.Año;
            }

            var countsAlumnosHist = new Dictionary<string, int>();
            if (clavesHist.Count > 0)
            {
                var gruposUnicos = clavesHist.Values.Select(k => k.Split('|')[0]).Distinct().ToList();
                var cuatriUnicos = clavesHist.Values.Select(k => k.Split('|')[1]).Distinct().ToList();
                var aniosUnicos = clavesHist.Values.Select(k => int.Parse(k.Split('|')[2])).Distinct().ToList();

                countsAlumnosHist = db.Individuals
                    .Where(i => gruposUnicos.Contains(i.Grupo)
                                && cuatriUnicos.Contains(i.Cuatrimestre)
                                && aniosUnicos.Contains(i.Fecha.Year))
                    .Select(i => new { i.IdPersona, i.Grupo, i.Cuatrimestre, Anio = i.Fecha.Year })
                    .ToList()
                    .GroupBy(x => x.Grupo + "|" + x.Cuatrimestre + "|" + x.Anio)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.IdPersona).Distinct().Count());
            }


            foreach (var item in solicitud)
            {
                // Buscar en el diccionario (incluye tutorías históricas, no solo las del periodo actual)
                TutoriaGrupal tuto;
                tutoriasLookup.TryGetValue(item.IdTutoriaGrupal, out tuto);
                if (tuto == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[PAT] ADVERTENCIA: No se encontró tutoría para IdTutoriaGrupal={item.IdTutoriaGrupal}");
                    continue; // Saltar este PAT si no tiene tutoría asociada
                }

                // --- INICIO NUEVO CÓDIGO ---
                // Determinar y guardar el nombre real del turno en el diccionario
                string nombreTurnoReal = todosLosTurnos.ContainsKey(tuto.IdTurno) ? todosLosTurnos[tuto.IdTurno] : "Sin turno";
                turnosPorPatId[item.IdEntrevistaInicial] = nombreTurnoReal;
                // --- FIN NUEVO CÓDIGO ---

                // Recalcular cantidad de alumnos:
                // - Histórico: usar el conteo precomputado desde Individuals.
                // - Vigente: desde DatosPersonales (activos).
                if (clavesHist.TryGetValue(item.IdEntrevistaInicial, out string claveHistPat))
                {
                    item.CantidadAlumno = countsAlumnosHist.TryGetValue(claveHistPat, out int cHist) ? cHist : 0;
                }
                else
                {
                    item.CantidadAlumno = db.DatosPersonales.Count(dp =>
                        dp.IdCarrera == tuto.IdCarrera &&
                        dp.IdGrado == tuto.IdGrado &&
                        dp.IdGrupo == tuto.IdGrupo &&
                        dp.IdTurno == tuto.IdTurno &&
                        dp.IdPeriodo == tuto.IdPeriodo &&
                        dp.Año == tuto.Año &&
                        dp.Estado == true);
                }



                // Generar nomenclatura del grupo (SIN la inicial del turno)
                var grupo = "";
                var carreraInfo = todasLasCarreras.ContainsKey(tuto.IdCarrera) ? todasLasCarreras[tuto.IdCarrera] : null; // Mantenemos carrera
                string nombreGrado = todosLosGrados.ContainsKey(tuto.IdGrado) ? todosLosGrados[tuto.IdGrado] : "";
                string nombreGrupoLetra = todosLosGruposLetra.ContainsKey(tuto.IdGrupo) ? todosLosGruposLetra[tuto.IdGrupo] : "";

                grupo += carreraInfo?.Nomenclatura ?? ""; // Añadir Nomenclatura Carrera
                grupo += nombreGrado ?? "";             // Añadir Grado
                grupo += nombreGrupoLetra ?? "";        // Añadir Letra Grupo

                item.TutoriaGrupal = grupo; // Nomenclatura sin inicial de turno

                // Asignar nombre del tutor desde el diccionario
                if (item.IdTutor > 0 && todosLosTutores.ContainsKey(item.IdTutor))
                {
                    item.Tutor = todosLosTutores[item.IdTutor];
                }
                else if (item.IdTutor > 0)
                {
                    // Si el Usuario asociado al IdTutor ya no existe (cuenta eliminada/reasignada),
                    // conservamos el nombre que quedó cacheado en el campo PAT.Tutor al crearse —
                    // es el snapshot real del tutor en ese cuatrimestre. Sólo mostramos
                    // "TUTOR NO ENCONTRADO" si tampoco hay valor cacheado.
                    if (string.IsNullOrWhiteSpace(item.Tutor))
                        item.Tutor = "TUTOR NO ENCONTRADO";
                    else
                        item.Tutor = item.Tutor.ToUpper();
                }
            }


            // ============================================
            // PASO 6: Ordenar correctamente: Grado -> Letra -> Turno
            // ============================================
            solicitud = solicitud
                .OrderByDescending(x => tutoriasLookup.ContainsKey(x.IdTutoriaGrupal) ? tutoriasLookup[x.IdTutoriaGrupal].Año : 0)
                .ThenByDescending(x => tutoriasLookup.ContainsKey(x.IdTutoriaGrupal) ? tutoriasLookup[x.IdTutoriaGrupal].IdPeriodo : 0)
                .ThenBy(x => tutoriasLookup.ContainsKey(x.IdTutoriaGrupal) ? tutoriasLookup[x.IdTutoriaGrupal].IdGrado : 999)
                .ThenBy(x => tutoriasLookup.ContainsKey(x.IdTutoriaGrupal) ? tutoriasLookup[x.IdTutoriaGrupal].IdGrupo : 999)
                .ThenBy(x => tutoriasLookup.ContainsKey(x.IdTutoriaGrupal) ? tutoriasLookup[x.IdTutoriaGrupal].IdTurno : 999)
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[PAT] Enviando {solicitud.Count} PATs a la vista");

            // Mensaje de éxito si se crearon PATs
            if (patsCreados > 0)
            {
                TempData["Success"] = $"Se crearon automáticamente {patsCreados} PATs para grupos que no tenían uno.";
            }
            // --- INICIO NUEVO CÓDIGO ---
            // Pasar el diccionario de turnos a la vista a través del ViewBag
            ViewBag.NombresTurno = turnosPorPatId;
            // --- FIN NUEVO CÓDIGO ---

            // ... (Código existente donde obtienes la lista 'solicitud') ...

            // --- INICIO LÓGICA ESTADÍSTICAS ---
            // Las estadísticas del encabezado se calculan sobre los PATs del periodo/año
            // actual (que es lo que la vista muestra por defecto). El listado completo
            // 'solicitud' incluye históricos para permitir filtrarlos en el cliente.
            var solicitudActual = solicitud
                .Where(p => p.IdPeriodo == periodoActual && p.Fecha.Year == añoActual)
                .ToList();

            if (usuario.IdNivel == 4) // Master
            {
                // Agrupar por nombre de carrera y contar
                var estadisticasCarrera = solicitudActual
                    .GroupBy(p => p.IdCarrera)
                    .Select(g => new {
                        IdCarrera = g.Key,
                        Count = g.Count()
                    }).ToList();

                // Crear diccionario con nombres legibles
                Dictionary<string, int> statsDisplay = new Dictionary<string, int>();
                foreach (var item in estadisticasCarrera)
                {
                    var carreraInfo = todasLasCarreras.ContainsKey(item.IdCarrera) ? todasLasCarreras[item.IdCarrera] : null;
                    string nombre = carreraInfo != null ? $"{carreraInfo.Nombre} ({carreraInfo.Nomenclatura})" : "Desconocida";
                    statsDisplay.Add(nombre, item.Count);
                }

                ViewBag.TotalGlobal = solicitudActual.Count;
                ViewBag.EstadisticasDetalladas = statsDisplay;
            }
            else // Coordinador
            {
                var carreraInfo = todasLasCarreras.ContainsKey(usuario.IdCarrera) ? todasLasCarreras[usuario.IdCarrera] : null;
                string nombreCarrera = carreraInfo != null ? $"{carreraInfo.Nombre} ({carreraInfo.Nomenclatura})" : "";

                ViewBag.ResumenCoordinador = $"PATs Totales de {nombreCarrera}: {solicitudActual.Count}";
            }
            // --- FIN LÓGICA ESTADÍSTICAS ---

            // Mapa Nombre → IdCarrera para que la vista pueda enviar IdCarrera al exportar reportes filtrados.
            ViewBag.CarrerasMap = todasLasCarreras.ToDictionary(kv => kv.Value.Nombre, kv => kv.Key);

            return View(solicitud);


        }

        // ============================================
        // NUEVO MÉTODO AUXILIAR
        // ============================================
        private PAT CrearPATParaTutoriaCoordinador(TutoriaGrupal tutoria, Usuario tutor, int periodoActual)
        {
            // Contar alumnos activos (sin bajas)
            var idsActivos = QueryAlumnosDelGrupoSinBajas(tutoria);
            int cantidadAlumnos = idsActivos.Count;

            // Calcular vulnerabilidades usando Seguimientos (lógica unificada con Tutor)
            var conteo = CalcularVulnerabilidadesPorSeguimiento(tutoria);

            var nuevoPAT = new PAT
            {
                IdTutor = tutor.IdUsuario,
                Tutor = tutor.NombreCompleto,
                IdTutoriaGrupal = tutoria.IdTutoriaGrupal,
                IdCarrera = tutoria.IdCarrera,
                IdPeriodo = periodoActual,
                Fecha = DateTime.Now,
                CantidadAlumno = cantidadAlumnos,
                estado = true,
                EstadoRevision = 0,
                VunerableEconomico = conteo.Economico,
                VunerablePersonal = conteo.Personal,
                VunerableAcademico = conteo.Academico,
                DescripcionEconomico = "",
                DescripcionPersonal = "",
                DescripcionAcademico = ""
            };

            return nuevoPAT;
        }
        //7777777
        [HttpGet]
        public JsonResult VerificarGruposEsperados()
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
                int añoActual = tiempo.Year;

                // Obtener la carrera del coordinador
                var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == usuario.IdCarrera);
                string nomenclaturaCarrera = carrera?.Nomenclatura ?? "??";

                // Obtener tutorías existentes
                var tutoriasExistentes = db.TutoriaGrupals
                    .Where(tg => tg.IdCarrera == usuario.IdCarrera
                              && tg.IdPeriodo == periodoActual
                              && tg.Año == añoActual)
                    .ToList();

                // Obtener todos los grados que existen en el sistema
                var gradosEnSistema = db.Gradoes.OrderBy(g => g.Nombre).ToList();

                // Obtener todos los grupos (letras) que existen
                var gruposEnSistema = db.Grupoes.OrderBy(g => g.Nombre).ToList();

                // Obtener turnos
                var turnos = db.Turnoes.ToList();

                // Generar matriz de grupos esperados vs existentes
                var analisis = new List<object>();

                foreach (var grado in gradosEnSistema.Take(10)) // Solo primeros 10 grados
                {
                    foreach (var grupo in gruposEnSistema.Take(10)) // Primeras 10 letras (A-J)
                    {
                        foreach (var turno in turnos)
                        {
                            string letraTurno = turno.Nombre == "Matutino" ? "M" :
                                               turno.Nombre == "Vespertino" ? "I" : "D";

                            string nombreGrupo = $"{letraTurno}{nomenclaturaCarrera}{grado.Nombre}{grupo.Nombre}";

                            var existe = tutoriasExistentes.Any(t =>
                                t.IdGrado == grado.IdGrado &&
                                t.IdGrupo == grupo.IdGrupo &&
                                t.IdTurno == turno.IdTurno);

                            var tutoria = tutoriasExistentes.FirstOrDefault(t =>
                                t.IdGrado == grado.IdGrado &&
                                t.IdGrupo == grupo.IdGrupo &&
                                t.IdTurno == turno.IdTurno);

                            var tutor = tutoria != null ?
                                db.Usuarios.FirstOrDefault(u => u.IdUsuario == tutoria.IdUsuario) : null;

                            analisis.Add(new
                            {
                                Grupo = nombreGrupo,
                                Grado = grado.Nombre,
                                Letra = grupo.Nombre,
                                Turno = turno.Nombre,
                                Existe = existe,
                                TutorAsignado = tutor?.NombreCompleto ?? "SIN ASIGNAR",
                                IdTutoriaGrupal = tutoria?.IdTutoriaGrupal
                            });
                        }
                    }
                }

                // Filtrar solo los grupos que NO existen pero que podrían existir
                var gruposFaltantes = analisis.Where(a => !(bool)a.GetType().GetProperty("Existe").GetValue(a)).ToList();

                // Agrupar por grado para ver patrones
                var analisisPorGrado = analisis
                    .GroupBy(a => a.GetType().GetProperty("Grado").GetValue(a))
                    .Select(g => new {
                        Grado = g.Key,
                        TotalPosibles = g.Count(),
                        Existentes = g.Count(x => (bool)x.GetType().GetProperty("Existe").GetValue(x)),
                        Faltantes = g.Count(x => !(bool)x.GetType().GetProperty("Existe").GetValue(x))
                    })
                    .OrderBy(x => x.Grado)
                    .ToList();

                return Json(new
                {
                    success = true,
                    carrera = nomenclaturaCarrera,
                    periodo = periodoActual,
                    año = añoActual,

                    // Resumen por grado
                    resumenPorGrado = analisisPorGrado,

                    // Grupos que existen
                    gruposExistentes = analisis
                        .Where(a => (bool)a.GetType().GetProperty("Existe").GetValue(a))
                        .OrderBy(a => a.GetType().GetProperty("Grupo").GetValue(a))
                        .Take(50), // Primeros 50

                    // Grupos que NO existen (posibles faltantes)
                    gruposPosiblementeFaltantes = gruposFaltantes.Take(30) // Primeros 30
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // MÉTODO DE DIAGNÓSTICO TEMPORAL - VERSIÓN CORREGIDA
        [HttpGet]
        public JsonResult DiagnosticarGruposFaltantes()
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
                int añoActual = tiempo.Year;

                // 1. Obtener todas las tutorías de la carrera (SIMPLE, sin navegaciones)
                var todasLasTutorias = db.TutoriaGrupals
                    .Where(tg => tg.IdCarrera == usuario.IdCarrera
                              && tg.IdPeriodo == periodoActual
                              && tg.Año == añoActual)
                    .ToList(); // <-- IMPORTANTE: ToList() primero

                // 2. Procesar en memoria
                var tutoriasConNombres = todasLasTutorias.Select(tg => {
                    var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == tg.IdGrado);
                    var grupo = db.Grupoes.FirstOrDefault(g => g.IdGrupo == tg.IdGrupo);
                    var turno = db.Turnoes.FirstOrDefault(t => t.IdTurno == tg.IdTurno);
                    var tutor = db.Usuarios.FirstOrDefault(u => u.IdUsuario == tg.IdUsuario);

                    return new
                    {
                        IdTutoriaGrupal = tg.IdTutoriaGrupal,
                        IdGrado = tg.IdGrado,
                        IdGrupo = tg.IdGrupo,
                        IdTurno = tg.IdTurno,
                        IdUsuario = tg.IdUsuario,
                        GradoNombre = grado?.Nombre ?? "?",
                        GrupoNombre = grupo?.Nombre ?? "?",
                        TurnoNombre = turno?.Nombre ?? "?",
                        TutorNombre = tutor?.NombreCompleto ?? "Sin tutor",
                        NombreCompleto = GenerarNombreGrupoParaDiagnostico(tg)
                    };
                }).ToList();

                // 3. Obtener PATs existentes
                var patsExistentes = db.PATs
                    .Where(p => p.IdCarrera == usuario.IdCarrera
                             && p.IdPeriodo == periodoActual
                             && p.Fecha.Year == añoActual)
                    .ToList();

                var patsConGrupo = patsExistentes.Select(p => {
                    var tuto = db.TutoriaGrupals.FirstOrDefault(t => t.IdTutoriaGrupal == p.IdTutoriaGrupal);
                    return new
                    {
                        IdTutoriaGrupal = p.IdTutoriaGrupal,
                        IdPAT = p.IdEntrevistaInicial,
                        Grupo = tuto != null ? GenerarNombreGrupoParaDiagnostico(tuto) : "?"
                    };
                }).ToList();

                // 4. Tutorías sin PAT
                var idsConPAT = patsExistentes.Select(p => p.IdTutoriaGrupal).ToList();
                var tutoriasSinPAT = tutoriasConNombres
                    .Where(t => !idsConPAT.Contains(t.IdTutoriaGrupal))
                    .OrderBy(t => t.GradoNombre)
                    .ThenBy(t => t.GrupoNombre)
                    .ToList();

                return Json(new
                {
                    success = true,
                    carreraId = usuario.IdCarrera,
                    periodo = periodoActual,
                    año = añoActual,
                    totalTutorias = todasLasTutorias.Count,
                    totalPATs = patsExistentes.Count,
                    tutoriasSinPAT = tutoriasSinPAT.Count,

                    // Grupos ordenados por grado y letra
                    tutoriasExistentes = tutoriasConNombres
                        .OrderBy(t => t.GradoNombre)
                        .ThenBy(t => t.GrupoNombre),

                    patsCreados = patsConGrupo
                        .OrderBy(p => p.Grupo),

                    tutoriasFaltantes = tutoriasSinPAT.Select(t => new {
                        t.NombreCompleto,
                        t.TutorNombre,
                        t.GradoNombre,
                        t.GrupoNombre,
                        t.TurnoNombre
                    })
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // Método auxiliar para generar nombre del grupo
        private string GenerarNombreGrupoParaDiagnostico(TutoriaGrupal tg)
        {
            var grupo = "";
            // ELIMINADO: var turno = db.Turnoes.FirstOrDefault(t => t.IdTurno == tg.IdTurno);
            var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == tg.IdCarrera); // Mantenemos carrera
            var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == tg.IdGrado);
            var grup = db.Grupoes.FirstOrDefault(g => g.IdGrupo == tg.IdGrupo);

            // --- INICIO MODIFICACIÓN ---
            grupo += carrera?.Nomenclatura ?? ""; // Añadir Nomenclatura Carrera
            grupo += grado?.Nombre ?? "";       // Añadir Grado
            grupo += grup?.Nombre ?? "";       // Añadir Letra Grupo
                                               // --- FIN MODIFICACIÓN ---

            return grupo;
        }

        // REEMPLAZA TU MÉTODO PatDetalles EXISTENTE CON ESTE:

        public ActionResult PatDetalles(int? id)
        {
            SetListas();
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null) return RedirectToAction("Login", "Account"); // Validación sesión
            ViewBag.IdNivel = usuario.IdNivel;

            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            PAT pAT = db.PATs.Find(id);
            if (pAT == null) return HttpNotFound();

            // --- SEGURIDAD: Validar que el Coordinador (Nivel 3) pertenezca a la misma carrera del PAT ---
            if (usuario.IdNivel == 3 && pAT.IdCarrera != usuario.IdCarrera)
            {
                TempData["Error"] = "No tiene permisos para acceder a este PAT.";
                return RedirectToAction("PAT");
            }

            var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == pAT.IdTutoriaGrupal);

            if (tuto != null)
            {
                // 1. Traer alumnos que están o estuvieron en el grupo
                var todosAlumnos = db.DatosPersonales.Where(dp =>
                    dp.IdCarrera == tuto.IdCarrera && dp.IdGrado == tuto.IdGrado &&
                    dp.IdGrupo == tuto.IdGrupo && dp.IdTurno == tuto.IdTurno &&
                    dp.IdPeriodo == tuto.IdPeriodo && dp.Año == tuto.Año
                ).ToList();

                // --- CORRECCIÓN CONTADOR BAJAS ---
                // En lugar de confiar solo en 'dp.Estado', verificamos si el alumno existe en la tabla 'Bajas'
                var idsAlumnos = todosAlumnos.Select(x => x.IdPersona).ToList();

                // Contar cuántos de estos alumnos tienen registro en la tabla Bajas
                int bajasReales = db.Bajas.Count(b => idsAlumnos.Contains(b.IdPersona) && b.Activo == true);

                // Actualizar ViewBag
                ViewBag.CantidadBajas = bajasReales;

                // El total de alumnos en lista (activos + bajas)
                pAT.CantidadAlumno = todosAlumnos.Count;

                // =========================================================================
                // CÁLCULO DE VULNERABILIDADES
                // - Para PATs del cuatrimestre vigente: usar DatosPersonales+Seguimientos
                //   excluyendo bajas (foto del estado actual).
                // - Para PATs históricos: usar Individuals+Seguimientos. DatosPersonales se
                //   sobrescribe cuatrimestre a cuatrimestre, por lo que no preserva el
                //   snapshot histórico; Individuals sí lo conserva (Grupo+Cuatrimestre+Fecha).
                // En ambos casos: se toma el PRIMER seguimiento por alumno dentro del rango
                // de fechas del periodo.
                // =========================================================================
                bool esHistorico = EsPatHistorico(tuto);
                if (esHistorico)
                {
                    var primerosHist = ObtenerPrimerosSeguimientosHistorico(tuto);
                    pAT.VunerableEconomico = primerosHist.Count(x => string.Equals(x.Vulnerabilidad, "Economico", StringComparison.OrdinalIgnoreCase));
                    pAT.VunerableAcademico = primerosHist.Count(x => string.Equals(x.Vulnerabilidad, "Academico", StringComparison.OrdinalIgnoreCase));
                    pAT.VunerablePersonal = primerosHist.Count(x => string.Equals(x.Vulnerabilidad, "Personal", StringComparison.OrdinalIgnoreCase));
                    ViewBag.NoVulnerablesCount = primerosHist.Count(x =>
                        string.Equals(x.Vulnerabilidad, "No vulnerable", StringComparison.OrdinalIgnoreCase));

                    // Sobrescribir conteos de alumnos y bajas con la "foto" histórica.
                    // Alumnos vienen de Individuals; las Bajas se filtran por grupo + rango
                    // de fechas del periodo del PAT para no contar bajas posteriores en
                    // otros grupos como si pertenecieran a este cuatrimestre.
                    var idsAlumnosHist = ObtenerIdsAlumnosHistorico(tuto);
                    pAT.CantidadAlumno = idsAlumnosHist.Count;
                    ViewBag.CantidadBajas = ObtenerIdsBajasHistorico(tuto).Count;
                }
                else
                {
                    var conteoVulnerabilidades = CalcularVulnerabilidadesPorSeguimiento(tuto);
                    pAT.VunerableEconomico = conteoVulnerabilidades.Economico;
                    pAT.VunerableAcademico = conteoVulnerabilidades.Academico;
                    pAT.VunerablePersonal = conteoVulnerabilidades.Personal;
                    ViewBag.NoVulnerablesCount = ContarNoVulnerablesPorSeguimiento(tuto);
                }
            }
            // --- FIN DE LA CORRECCIÓN ---

            var grupo = "";
            // ELIMINADO: var t = db.Turnoes.FirstOrDefault(a => a.IdTurno == tuto.IdTurno);
            var c = db.Carreras.FirstOrDefault(a => a.IdCarrera == tuto.IdCarrera); // Mantenemos carrera
            var grado = db.Gradoes.FirstOrDefault(a => a.IdGrado == tuto.IdGrado);
            var grup = db.Grupoes.FirstOrDefault(a => a.IdGrupo == tuto.IdGrupo);

            // --- INICIO MODIFICACIÓN ---
            // Construir el nombre del grupo: Nomenclatura Carrera + Grado + Letra Grupo
            grupo += c?.Nomenclatura ?? ""; // Añadir Nomenclatura Carrera
            grupo += grado?.Nombre ?? "";    // Añadir Grado
            grupo += grup?.Nombre ?? "";    // Añadir Letra Grupo
                                            // --- FIN MODIFICACIÓN ---
                                            // --- FIN MODIFICACIÓN ---
            pAT.TutoriaGrupal = grupo;
            pAT.Grupo = grupo;

            if (pAT.IdTutor > 0)
            {
                var usuarioTutor = db.Usuarios.FirstOrDefault(u => u.IdUsuario == pAT.IdTutor);
                if (usuarioTutor != null)
                {
                    // Asignamos directamente NombreCompleto, que es el campo que sí existe
                    pAT.Tutor = (usuarioTutor.NombreCompleto ?? "").ToUpper();
                }
            }
            // En AsignarAsesoresController.cs, dentro de PatDetalles(int? id)
            // En AsignarAsesoresController.cs, dentro de PatDetalles(int? id)
            var actividades = db.actividadesSemanals
                                .Include(a => a.Semana) // <--- Asegúrate que está
                                .Include(a => a.Tipo)   // <--- Añade este también
                                .Where(x => x.IdEntrevistaInicial == id)
                                .ToList(); // ToList() después de los Include y Where
            ViewBag.Actividades = actividades.OrderBy(f => f.IdSemana).ToList(); // Ordenar después
            ViewBag.PAT = pAT;
            ViewBag.UsuarioActual = usuario.NombreCompleto;
            ViewBag.UserName = User.Identity.Name ?? usuario.UserName;
            return View(pAT);
        }


        [HttpGet]
        public JsonResult ObtenerAlumnosBajas(int patId)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Sesión expirada" }, JsonRequestBehavior.AllowGet);
                }

                var pat = db.PATs.Find(patId);
                if (pat == null) return Json(new { success = false, message = "PAT no encontrado" }, JsonRequestBehavior.AllowGet);

                // Seguridad: Validar que el coordinador pueda ver este PAT (misma carrera)
                // O si es nivel 4 (Master)
                if (usuario.IdNivel == 3 && pat.IdCarrera != usuario.IdCarrera)
                {
                    return Json(new { success = false, message = "No autorizado para ver este PAT" }, JsonRequestBehavior.AllowGet);
                }

                var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == pat.IdTutoriaGrupal);
                if (tuto == null) return Json(new { success = false, message = "Grupo no encontrado" }, JsonRequestBehavior.AllowGet);

                // 1. Obtener IDs de alumnos del grupo.
                // - Vigente: por filtros de cuatrimestre/grupo en DatosPersonales.
                // - Histórico: desde Individuals (snapshot del cuatrimestre), porque
                //   DatosPersonales no preserva la asignación de cuatrimestres pasados.
                List<int> alumnosDelGrupo = EsPatHistorico(tuto)
                    ? ObtenerIdsAlumnosHistorico(tuto)
                    : db.DatosPersonales.Where(dp =>
                        dp.IdCarrera == tuto.IdCarrera && dp.IdGrado == tuto.IdGrado &&
                        dp.IdGrupo == tuto.IdGrupo && dp.IdTurno == tuto.IdTurno &&
                        dp.IdPeriodo == tuto.IdPeriodo && dp.Año == tuto.Año
                      ).Select(x => x.IdPersona).ToList();

                // 2. Buscar las bajas correspondientes al PAT.
                //    Histórico: sólo las dadas en el grupo del PAT durante su rango de fechas
                //               (evita contar bajas posteriores del mismo alumno en otros grupos).
                //    Vigente:   todas las bajas activas de los alumnos del grupo.
                IQueryable<Baja> bajasQuery;
                if (EsPatHistorico(tuto))
                {
                    string grupoNomHist = BuildGrupoNomenclatura(tuto);
                    int anoHist = tuto.Año;
                    DateTime fechaInicioHist, fechaFinExcl;
                    if (tuto.IdPeriodo == 1) { fechaInicioHist = new DateTime(anoHist, 1, 1); fechaFinExcl = new DateTime(anoHist, 5, 1); }
                    else if (tuto.IdPeriodo == 2) { fechaInicioHist = new DateTime(anoHist, 5, 1); fechaFinExcl = new DateTime(anoHist, 9, 1); }
                    else { fechaInicioHist = new DateTime(anoHist, 9, 1); fechaFinExcl = new DateTime(anoHist + 1, 1, 1); }

                    bajasQuery = db.Bajas.Where(b => b.Activo == true
                                                  && b.Grupo == grupoNomHist
                                                  && b.Fecha >= fechaInicioHist
                                                  && b.Fecha < fechaFinExcl);
                }
                else
                {
                    bajasQuery = db.Bajas.Where(b => alumnosDelGrupo.Contains(b.IdPersona) && b.Activo == true);
                }

                // Join con DatosPersonales para nombre/matricula/foto. Si no encuentra
                // (alumno ya no está en DatosPersonales), usamos los datos de la propia Baja.
                var bajasRaw = bajasQuery
                    .Select(b => new { b.IdPersona, BajaNombre = b.Nombre, BajaMatricula = b.Matricula })
                    .ToList();

                var idsBaja = bajasRaw.Select(b => b.IdPersona).Distinct().ToList();
                var datosBaja = db.DatosPersonales
                    .Where(dp => idsBaja.Contains(dp.IdPersona))
                    .Select(dp => new { dp.IdPersona, dp.Nombre, dp.Matricula, dp.Email, dp.Foto, dp.Año, dp.IdPeriodo })
                    .ToList()
                    .GroupBy(dp => dp.IdPersona)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Año).ThenByDescending(x => x.IdPeriodo).First());

                var listaBajas = bajasRaw
                    .GroupBy(b => b.IdPersona)
                    .Select(g => g.First())
                    .Select(b =>
                    {
                        if (datosBaja.TryGetValue(b.IdPersona, out var dp))
                        {
                            return new
                            {
                                idPersona = b.IdPersona,
                                nombre = dp.Nombre,
                                matricula = dp.Matricula,
                                email = dp.Email,
                                foto = dp.Foto
                            };
                        }
                        return new
                        {
                            idPersona = b.IdPersona,
                            nombre = b.BajaNombre,
                            matricula = b.BajaMatricula,
                            email = (string)null,
                            foto = (string)null
                        };
                    })
                    .ToList();

                // Procesar fotos
                var resultado = listaBajas.Select(lb => new {
                    lb.idPersona,
                    lb.nombre,
                    lb.matricula,
                    lb.email,
                    foto = ProcesarFotoUniversal(lb.foto)
                }).OrderBy(x => x.nombre).ToList();

                // --- CORRECCIÓN DEL ERROR 500 ---
                // Usamos JsonResult con MaxJsonLength al máximo para soportar las fotos
                var jsonResult = new JsonResult
                {
                    Data = new { success = true, alumnos = resultado },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    MaxJsonLength = Int32.MaxValue // <--- ESTO SOLUCIONA EL ERROR DE CONEXIÓN
                };

                return jsonResult;
            }
            catch (Exception ex)
            {
                // Log en consola de Visual Studio para depuración
                System.Diagnostics.Debug.WriteLine("Error en ObtenerAlumnosBajas: " + ex.Message);
                return Json(new { success = false, message = "Error interno: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpGet]
        public ActionResult ObtenerTutoresSinPAT()
        {
            try
            {
                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
                int añoActual = tiempo.Year;

                var tutoresConGrupos = (from tutor in db.Usuarios
                                        join tutoriaGrupal in db.TutoriaGrupals on tutor.IdUsuario equals tutoriaGrupal.IdUsuario
                                        where tutor.IdNivel == 2
                                              && tutoriaGrupal.IdPeriodo == periodoActual
                                              && tutoriaGrupal.Año == añoActual
                                        select new
                                        {
                                            Tutor = tutor,
                                            TutoriaGrupal = tutoriaGrupal
                                        }).ToList();

                var patsExistentes = db.PATs.Where(p => p.IdPeriodo == periodoActual && p.Fecha.Year == añoActual)
                                           .Select(p => p.IdTutoriaGrupal).ToList();

                var tutoresSinPAT = tutoresConGrupos
                    .Where(tc => !patsExistentes.Contains(tc.TutoriaGrupal.IdTutoriaGrupal))
                    .GroupBy(tc => tc.Tutor.IdUsuario)
                    .Select(group => new {
                        IdTutor = group.Key,
                        NombreTutor = group.First().Tutor.NombreCompleto,
                        Email = group.First().Tutor.UserName,
                        CantidadGrupos = group.Count(),
                        Grupos = group.Select(tc => new {
                            IdTutoriaGrupal = tc.TutoriaGrupal.IdTutoriaGrupal,
                            NombreGrupo = GenerarNomenclaturaGrupoParaTutoria(tc.TutoriaGrupal),
                            IdCarrera = tc.TutoriaGrupal.IdCarrera,
                            NombreCarrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == tc.TutoriaGrupal.IdCarrera).Nombre
                        }).ToList()
                    }).ToList();

                return Json(new
                {
                    success = true,
                    tutores = tutoresSinPAT,
                    periodoActual = db.Periodos.FirstOrDefault(p => p.IdPeriodo == periodoActual)?.Nombre ?? "Período " + periodoActual,
                    añoActual = añoActual
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al obtener tutores: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearPATsManuales(List<int> tutorIds)
        {
            try
            {
                if (tutorIds == null || !tutorIds.Any())
                {
                    return Json(new { success = false, message = "No se seleccionaron tutores." });
                }

                var tiempo = DateTime.Now;
                int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 : (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
                int añoActual = tiempo.Year;

                int patsCreados = 0;
                List<string> errores = new List<string>();

                foreach (var tutorId in tutorIds)
                {
                    try
                    {
                        var tutor = db.Usuarios.FirstOrDefault(u => u.IdUsuario == tutorId && u.IdNivel == 2);
                        if (tutor == null)
                        {
                            errores.Add($"Tutor con ID {tutorId} no encontrado.");
                            continue;
                        }

                        var tutorias = db.TutoriaGrupals.Where(tg => tg.IdUsuario == tutorId
                                                                    && tg.IdPeriodo == periodoActual
                                                                    && tg.Año == añoActual).ToList();

                        var patsExistentes = db.PATs.Where(p => p.IdTutor == tutorId
                                                               && p.IdPeriodo == periodoActual
                                                               && p.Fecha.Year == añoActual)
                                                    .Select(p => p.IdTutoriaGrupal).ToList();

                        var tutoriasSinPAT = tutorias.Where(t => !patsExistentes.Contains(t.IdTutoriaGrupal)).ToList();

                        foreach (var tutoria in tutoriasSinPAT)
                        {
                            var nuevoPAT = CrearPATParaTutoriaManual(tutoria, tutor, periodoActual);
                            db.PATs.Add(nuevoPAT);
                            patsCreados++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errores.Add($"Error al crear PAT para tutor {tutorId}: {ex.Message}");
                    }
                }

                if (patsCreados > 0)
                {
                    db.SaveChanges();
                }

                return Json(new
                {
                    success = true,
                    message = $"Se crearon {patsCreados} PATs exitosamente.",
                    patsCreados = patsCreados,
                    errores = errores
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error general: " + ex.Message });
            }
        }

        private string GenerarNomenclaturaGrupoParaTutoria(TutoriaGrupal tutoria)
        {
            // --- INICIO DE LA CORRECCIÓN: Manejar si tutoria es nulo ---
            if (tutoria == null) return "SIN GRUPO";
            // --- FIN DE LA CORRECCIÓN ---

            // ELIMINADO: var turno = db.Turnoes.FirstOrDefault(t => t.IdTurno == tutoria.IdTurno);
            var carrera = db.Carreras.FirstOrDefault(c => c.IdCarrera == tutoria.IdCarrera); // Mantenemos carrera
            var grado = db.Gradoes.FirstOrDefault(g => g.IdGrado == tutoria.IdGrado);
            var grupo = db.Grupoes.FirstOrDefault(g => g.IdGrupo == tutoria.IdGrupo);

            string nomenclatura = "";

            // --- INICIO MODIFICACIÓN ---
            // Usamos '?' (null-conditional operator) para seguridad
            nomenclatura += carrera?.Nomenclatura ?? ""; // Añadir Nomenclatura Carrera
            nomenclatura += grado?.Nombre ?? "";       // Añadir Grado
            nomenclatura += grupo?.Nombre ?? "";       // Añadir Letra Grupo
                                                       // --- FIN MODIFICACIÓN ---

            return nomenclatura;
        }

        private PAT CrearPATParaTutoriaManual(TutoriaGrupal tutoria, Usuario tutor, int periodoActual)
        {
            int cantidadAlumnos = db.DatosPersonales.Count(dp => dp.IdCarrera == tutoria.IdCarrera
                                                                && dp.IdGrado == tutoria.IdGrado
                                                                && dp.IdGrupo == tutoria.IdGrupo
                                                                && dp.IdTurno == tutoria.IdTurno
                                                                && dp.IdPeriodo == tutoria.IdPeriodo
                                                                && dp.Año == tutoria.Año
                                                                && dp.Estado == true);

            var nuevoPAT = new PAT
            {
                IdTutor = tutor.IdUsuario,
                Tutor = tutor.NombreCompleto,
                IdTutoriaGrupal = tutoria.IdTutoriaGrupal,
                IdCarrera = tutoria.IdCarrera,
                IdPeriodo = periodoActual,
                Fecha = DateTime.Now,
                CantidadAlumno = cantidadAlumnos,
                estado = true,
                EstadoRevision = 0,
                VunerableEconomico = 0,
                VunerablePersonal = 0,
                VunerableAcademico = 0,
                DescripcionEconomico = "",
                DescripcionPersonal = "",
                DescripcionAcademico = ""
            };

            return nuevoPAT;
        }

        public ActionResult EliminarPAT(int? id)
        {
            PAT pAT = new PAT();
            PAT solicitud = db.PATs.FirstOrDefault(x => x.IdEntrevistaInicial == id);

            var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == solicitud.IdTutoriaGrupal);
            var grupo = "";
            var t = db.Turnoes.FirstOrDefault(a => a.IdTurno == tuto.IdTurno);
            var c = db.Carreras.FirstOrDefault(a => a.IdCarrera == tuto.IdCarrera);
            var grado = db.Gradoes.FirstOrDefault(a => a.IdGrado == tuto.IdGrado);
            var grup = db.Grupoes.FirstOrDefault(a => a.IdGrupo == tuto.IdGrupo);

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

            solicitud.TutoriaGrupal = grupo.ToString();

            return View(solicitud);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarPAT(int id)
        {
            PAT pat = db.PATs.Find(id);
            bool eliminado = false;
            if (pat != null)
            {
                db.PATs.Remove(pat);
                db.SaveChanges();
                eliminado = true;
            }
            if (Request.IsAjaxRequest())
            {
                return Json(new { success = eliminado });
            }
            return RedirectToAction("PAT");
        }

        public ActionResult PatReporte(int id, PAT pAT)
        {
            Usuario usuario = Session["Usuario"] as Usuario;
            if (usuario == null) return RedirectToAction("Login", "Account");

            // 1. Obtener el PAT principal
            PAT patPrincipal = db.PATs.FirstOrDefault(x => x.IdEntrevistaInicial == id);
            if (patPrincipal == null) return HttpNotFound();

            // --- SEGURIDAD: Validar permisos ---
            if (usuario.IdNivel == 3 && patPrincipal.IdCarrera != usuario.IdCarrera)
            {
                TempData["Error"] = "No tiene permisos para generar este reporte.";
                return RedirectToAction("PAT");
            }

            // 2. Obtener su tutoría asociada
            var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == patPrincipal.IdTutoriaGrupal);

            // 3. Ajustar datos si la tutoría existe
            if (tuto != null)
            {
                // Histórico: el snapshot del cuatrimestre se reconstruye desde Individuals
                // (DatosPersonales no preserva estado de cuatrimestres pasados).
                if (EsPatHistorico(tuto))
                {
                    var idsAlumnosHist = ObtenerIdsAlumnosHistorico(tuto);
                    patPrincipal.CantidadAlumno = idsAlumnosHist.Count;

                    var primerosHist = ObtenerPrimerosSeguimientosHistorico(tuto);
                    patPrincipal.VunerableEconomico = primerosHist.Count(x => string.Equals(x.Vulnerabilidad, "Economico", StringComparison.OrdinalIgnoreCase));
                    patPrincipal.VunerableAcademico = primerosHist.Count(x => string.Equals(x.Vulnerabilidad, "Academico", StringComparison.OrdinalIgnoreCase));
                    patPrincipal.VunerablePersonal = primerosHist.Count(x => string.Equals(x.Vulnerabilidad, "Personal", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    // 3a. Recalcular cantidad de alumnos activos (sin bajas)
                    var idsActivos = QueryAlumnosDelGrupoSinBajas(tuto);
                    patPrincipal.CantidadAlumno = idsActivos.Count;

                    // Vulnerabilidades (primer seguimiento del periodo actual, sin bajas)
                    var conteoVulnerabilidades = CalcularVulnerabilidadesPorSeguimiento(tuto);
                    patPrincipal.VunerableEconomico = conteoVulnerabilidades.Economico;
                    patPrincipal.VunerableAcademico = conteoVulnerabilidades.Academico;
                    patPrincipal.VunerablePersonal = conteoVulnerabilidades.Personal;
                }
            }

            // 3c. Corregir el nombre del tutor (para jalar el nombre completo si falta)
            if (patPrincipal.IdTutor > 0)
            {
                var usuarioTutor = db.Usuarios.FirstOrDefault(u => u.IdUsuario == patPrincipal.IdTutor);
                if (usuarioTutor != null)
                {
                    patPrincipal.Tutor = (usuarioTutor.NombreCompleto ?? "").ToUpper();
                }
            }

            // 4. Poner el PAT actualizado en la lista 'solicitud' para el reporte
            List<PAT> solicitud = new List<PAT>();
            solicitud.Add(patPrincipal);

            // 5. Obtener actividades y procesar para el reporte (Mayúsculas, X en semanas, etc.)
            List<ActividadesSemanal> actividades = db.actividadesSemanals.Where(x => x.IdEntrevistaInicial == patPrincipal.IdEntrevistaInicial).ToList();

            foreach (ActividadesSemanal x in actividades)
            {
                PAT aux = new PAT();

                // Marcar la semana correspondiente con una "X"
                if (x.IdSemana == 1) aux.Semana1 = "X";
                else if (x.IdSemana == 2) aux.Semana2 = "X";
                else if (x.IdSemana == 3) aux.Semana3 = "X";
                else if (x.IdSemana == 4) aux.Semana4 = "X";
                else if (x.IdSemana == 5) aux.Semana5 = "X";
                else if (x.IdSemana == 6) aux.Semana6 = "X";
                else if (x.IdSemana == 7) aux.Semana7 = "X";
                else if (x.IdSemana == 8) aux.Semana8 = "X";
                else if (x.IdSemana == 9) aux.Semana9 = "X";
                else if (x.IdSemana == 10) aux.Semana10 = "X";
                else if (x.IdSemana == 11) aux.Semana11 = "X";
                else if (x.IdSemana == 12) aux.Semana12 = "X";
                else if (x.IdSemana == 13) aux.Semana13 = "X";
                else if (x.IdSemana == 14) aux.Semana14 = "X";
                else if (x.IdSemana == 15) aux.Semana15 = "X";
                else aux.Semana16 = "X";

                // Marcar tipo de tutoría
                if (x.IdTipoTutoria == 1) aux.TipoTutoria1 = "X";
                else aux.TipoTutoria2 = "X";

                // Marcar realizado
                aux.RealizoActividad = (x.RealizoActividad == true) ? "SI" : "NO";

                // Convertir textos a Mayúsculas
                aux.Comentarios = (x.Comentarios ?? "").ToUpper();
                aux.Actividad = (x.Actividad ?? "").ToUpper();

                // Copiar el periodo del padre para que no salga vacío en la fila (opcional pero seguro)
                aux.IdPeriodo = patPrincipal.IdPeriodo;

                solicitud.Add(aux);
            }

            try
            {
                // 6. Llenar datos complementarios en mayúsculas
                foreach (PAT v in solicitud)
                {
                    Carrera carrera = db.Carreras.Find(v.IdCarrera);
                    if (carrera != null)
                    {
                        v.Carreras = (carrera.Nombre ?? "").ToUpper();

                        Periodo periodo = db.Periodos.Find(v.IdPeriodo);
                        if (periodo != null)
                        {
                            v.Periodos = (periodo.Nombre ?? "").ToUpper();
                        }

                        // Usar el mismo objeto 'tuto' si coincide ID, o buscar de nuevo
                        var tutoLocal = (v.IdTutoriaGrupal == patPrincipal.IdTutoriaGrupal) ? tuto : db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == v.IdTutoriaGrupal);
                        v.Grupo = GenerarNomenclaturaGrupoParaTutoria(tutoLocal).ToUpper();

                        var cuatr = db.Periodos.FirstOrDefault(x => x.IdPeriodo == v.IdPeriodo);
                        if (cuatr != null)
                        {
                            v.Cuatrimestre = (cuatr.Nombre ?? "").ToUpper();
                        }

                        if (!string.IsNullOrEmpty(v.Tutor))
                        {
                            v.Tutor = v.Tutor.ToUpper();
                        }
                    }
                }

                // 7. Configurar ReportViewer
                ReportViewer report1 = new ReportViewer();
                ReportDataSource rds = new ReportDataSource();
                rds.Value = solicitud;
                rds.Name = "DataSet1";
                report1.LocalReport.DataSources.Add(rds);
                report1.LocalReport.ReportPath = Server.MapPath("~/Reporte/ReportePAT.rdlc");
                ViewBag.ReportViewer = report1;
            }
            catch (Exception ex)
            {
                ViewBag.Mensaje = ex.Message;
            }

            return View();
        }

        #region Corrected Vulnerability Logic

        [HttpGet]
        public JsonResult ObtenerAlumnosVulnerables(int patId, int tipoVulnerabilidad)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                var pat = db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == patId);
                if (pat == null)
                {
                    return Json(new { success = false, message = "PAT no encontrado" }, JsonRequestBehavior.AllowGet);
                }

                var tutoriaGrupal = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == pat.IdTutoriaGrupal);
                if (tutoriaGrupal == null)
                {
                    return Json(new { success = false, message = "Tutoría grupal no encontrada" }, JsonRequestBehavior.AllowGet);
                }

                // Mapear tipoVulnerabilidad (1,2,3) a string
                string tipoVulnString = tipoVulnerabilidad == 1 ? "Economico" :
                                        tipoVulnerabilidad == 2 ? "Academico" : "Personal";

                // Obtener IDs de alumnos vulnerables por su PRIMER seguimiento del periodo.
                // Para PATs históricos usamos Individuals (snapshot real del cuatrimestre);
                // para PATs vigentes mantenemos el camino existente vía DatosPersonales+Seguimientos.
                List<int> idsVulnerables;
                if (EsPatHistorico(tutoriaGrupal))
                {
                    idsVulnerables = ObtenerPrimerosSeguimientosHistorico(tutoriaGrupal)
                        .Where(x => string.Equals(x.Vulnerabilidad, tipoVulnString, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.IdPersona)
                        .Distinct()
                        .ToList();
                }
                else
                {
                    idsVulnerables = ObtenerIdsVulnerablesPorSeguimiento(tutoriaGrupal, tipoVulnString);
                }

                // Datos del alumno para el modal (nombre/matrícula/email/foto).
                // Para PATs históricos buscamos por IdPersona sin filtros de cuatrimestre/baja:
                // DatosPersonales sólo guarda el estado vigente, así que tomamos lo que haya
                // ahora (lo único disponible). Si el alumno ya no existe ahí, lo enriquecemos
                // con lo guardado en Individuals (sin foto/email).
                List<AlumnoVulnRow> alumnosEnGrupo;
                if (EsPatHistorico(tutoriaGrupal))
                {
                    // Materializar primero y agrupar en memoria — evita problemas de traducción
                    // SQL en EF6 con GroupBy + OrderBy + FirstOrDefault.
                    var datosRaw = db.DatosPersonales
                        .Where(dp => idsVulnerables.Contains(dp.IdPersona))
                        .Select(dp => new { dp.IdPersona, dp.Nombre, dp.Matricula, dp.Email, dp.Foto, dp.Año, dp.IdPeriodo })
                        .ToList();

                    var desdeDatos = datosRaw
                        .GroupBy(dp => dp.IdPersona)
                        .Select(g => g.OrderByDescending(dp => dp.Año).ThenByDescending(dp => dp.IdPeriodo).First())
                        .Select(dp => new AlumnoVulnRow
                        {
                            IdPersona = dp.IdPersona,
                            Nombre = dp.Nombre,
                            Matricula = dp.Matricula,
                            Email = dp.Email,
                            Foto = dp.Foto
                        })
                        .ToList();

                    var idsConDatos = new HashSet<int>(desdeDatos.Select(d => d.IdPersona));
                    var faltantes = idsVulnerables.Where(id => !idsConDatos.Contains(id)).ToList();

                    List<AlumnoVulnRow> desdeIndividuals;
                    if (faltantes.Count == 0)
                    {
                        desdeIndividuals = new List<AlumnoVulnRow>();
                    }
                    else
                    {
                        var indRaw = db.Individuals
                            .Where(i => faltantes.Contains(i.IdPersona))
                            .Select(i => new { i.IdPersona, i.Nombre, i.Matricula, i.Fecha })
                            .ToList();

                        desdeIndividuals = indRaw
                            .GroupBy(i => i.IdPersona)
                            .Select(g => g.OrderByDescending(x => x.Fecha).First())
                            .Select(i => new AlumnoVulnRow
                            {
                                IdPersona = i.IdPersona,
                                Nombre = i.Nombre,
                                Matricula = i.Matricula,
                                Email = null,
                                Foto = null
                            })
                            .ToList();
                    }

                    alumnosEnGrupo = desdeDatos.Concat(desdeIndividuals).ToList();
                }
                else
                {
                    alumnosEnGrupo = QueryAlumnosDelGrupo(tutoriaGrupal)
                        .Where(dp => idsVulnerables.Contains(dp.IdPersona))
                        .Select(dp => new AlumnoVulnRow
                        {
                            IdPersona = dp.IdPersona,
                            Nombre = dp.Nombre,
                            Matricula = dp.Matricula,
                            Email = dp.Email,
                            Foto = dp.Foto
                        })
                        .Distinct()
                        .ToList();
                }

                var resultado = alumnosEnGrupo
                    .Select(a => new
                    {
                        idPersona = a.IdPersona,
                        nombre = a.Nombre ?? "Sin nombre",
                        matricula = a.Matricula ?? "Sin matrícula",
                        email = a.Email ?? "",
                        foto = ProcesarFotoUniversal(a.Foto)
                    })
                    .OrderBy(a => a.nombre)
                    .ToList();

                // Limitar a 50 alumnos si hay demasiados
                if (resultado.Count > 50)
                {
                    System.Diagnostics.Debug.WriteLine($"[ObtenerAlumnosVulnerables] Limitando de {resultado.Count} a 50 alumnos");
                    resultado = resultado.Take(50).ToList();
                }

                var jsonResult = new JsonResult
                {
                    Data = new { success = true, alumnos = resultado, total = resultado.Count },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    MaxJsonLength = Int32.MaxValue
                };
                return jsonResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR en ObtenerAlumnosVulnerables: {ex.ToString()}");
                return Json(new { success = false, message = "Error de conexión. Por favor, inténtelo de nuevo." }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult ObtenerAlumnosNoVulnerables(int patId)
        {
            try
            {
                Usuario usuario = Session["Usuario"] as Usuario;
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Sesión expirada o usuario no autenticado" }, JsonRequestBehavior.AllowGet);
                }

                var pat = db.PATs.FirstOrDefault(p => p.IdEntrevistaInicial == patId);
                if (pat == null)
                {
                    return Json(new { success = false, message = "PAT no encontrado" }, JsonRequestBehavior.AllowGet);
                }

                var tutoriaGrupal = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == pat.IdTutoriaGrupal);
                if (tutoriaGrupal == null)
                {
                    return Json(new { success = false, message = "Tutoría grupal no encontrada" }, JsonRequestBehavior.AllowGet);
                }

                // IDs de alumnos no vulnerables (primer seguimiento = "No vulnerable").
                // - Histórico: usar Individuals (snapshot real del cuatrimestre).
                // - Vigente: mantener camino existente vía DatosPersonales + Seguimientos
                //   (excluyendo bajas).
                List<int> idsNoVulnerablesTotales;
                List<AlumnoVulnRow> alumnosEnGrupo;

                if (EsPatHistorico(tutoriaGrupal))
                {
                    idsNoVulnerablesTotales = ObtenerPrimerosSeguimientosHistorico(tutoriaGrupal)
                        .Where(x => string.Equals(x.Vulnerabilidad, "No vulnerable", StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.IdPersona)
                        .Distinct()
                        .ToList();

                    // Datos del alumno: DatosPersonales por IdPersona (estado vigente) y
                    // fallback a Individuals para los que ya no estén en DatosPersonales.
                    var datosRaw = db.DatosPersonales
                        .Where(dp => idsNoVulnerablesTotales.Contains(dp.IdPersona))
                        .Select(dp => new { dp.IdPersona, dp.Nombre, dp.Matricula, dp.Email, dp.Foto, dp.Año, dp.IdPeriodo })
                        .ToList();

                    var desdeDatos = datosRaw
                        .GroupBy(dp => dp.IdPersona)
                        .Select(g => g.OrderByDescending(dp => dp.Año).ThenByDescending(dp => dp.IdPeriodo).First())
                        .Select(dp => new AlumnoVulnRow
                        {
                            IdPersona = dp.IdPersona,
                            Nombre = dp.Nombre,
                            Matricula = dp.Matricula,
                            Email = dp.Email,
                            Foto = dp.Foto
                        })
                        .ToList();

                    var idsConDatos = new HashSet<int>(desdeDatos.Select(d => d.IdPersona));
                    var faltantes = idsNoVulnerablesTotales.Where(id => !idsConDatos.Contains(id)).ToList();

                    List<AlumnoVulnRow> desdeIndividuals;
                    if (faltantes.Count == 0)
                    {
                        desdeIndividuals = new List<AlumnoVulnRow>();
                    }
                    else
                    {
                        var indRaw = db.Individuals
                            .Where(i => faltantes.Contains(i.IdPersona))
                            .Select(i => new { i.IdPersona, i.Nombre, i.Matricula, i.Fecha })
                            .ToList();

                        desdeIndividuals = indRaw
                            .GroupBy(i => i.IdPersona)
                            .Select(g => g.OrderByDescending(x => x.Fecha).First())
                            .Select(i => new AlumnoVulnRow
                            {
                                IdPersona = i.IdPersona,
                                Nombre = i.Nombre,
                                Matricula = i.Matricula,
                                Email = null,
                                Foto = null
                            })
                            .ToList();
                    }

                    alumnosEnGrupo = desdeDatos.Concat(desdeIndividuals).ToList();
                }
                else
                {
                    // Obtener todos los alumnos activos del grupo (DISTINTOS por IdPersona)
                    var alumnosLista = QueryAlumnosDelGrupo(tutoriaGrupal)
                        .GroupBy(dp => dp.IdPersona)
                        .Select(g => g.FirstOrDefault())
                        .Select(dp => new AlumnoVulnRow
                        {
                            IdPersona = dp.IdPersona,
                            Nombre = dp.Nombre,
                            Matricula = dp.Matricula,
                            Email = dp.Email,
                            Foto = dp.Foto
                        })
                        .ToList();

                    var idsTodos = alumnosLista.Select(x => x.IdPersona).ToList();

                    var idsBajasReales = db.Bajas
                        .Where(b => idsTodos.Contains(b.IdPersona) && b.Activo == true)
                        .Select(b => b.IdPersona)
                        .ToList();

                    var idsAlumnosActivos = idsTodos.Except(idsBajasReales).ToList();

                    int ano = tutoriaGrupal.Año;
                    int idPeriodo = tutoriaGrupal.IdPeriodo;
                    DateTime fechaInicio, fechaFin;
                    if (idPeriodo == 1) { fechaInicio = new DateTime(ano, 1, 1); fechaFin = new DateTime(ano, 4, 30); }
                    else if (idPeriodo == 2) { fechaInicio = new DateTime(ano, 5, 1); fechaFin = new DateTime(ano, 8, 31); }
                    else { fechaInicio = new DateTime(ano, 9, 1); fechaFin = new DateTime(ano, 12, 31); }

                    var seguimientos = (from s in db.Seguimientoes
                                        join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                        where idsAlumnosActivos.Contains(i.IdPersona) &&
                                              s.Fecha >= fechaInicio && s.Fecha <= fechaFin
                                        select new { i.IdPersona, s.Vulnerabilidad, s.Fecha, s.IdSeguimiento })
                                       .ToList();

                    var primerosSeguimientos = seguimientos
                        .GroupBy(x => x.IdPersona)
                        .Select(g => g.OrderBy(f => f.Fecha).ThenBy(f => f.IdSeguimiento).FirstOrDefault())
                        .ToList();

                    idsNoVulnerablesTotales = primerosSeguimientos
                        .Where(x => x != null && string.Equals(x.Vulnerabilidad, "No vulnerable", StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.IdPersona)
                        .ToList();

                    alumnosEnGrupo = alumnosLista;
                }

                var resultadoFinal = alumnosEnGrupo
                    .Where(a => idsNoVulnerablesTotales.Contains(a.IdPersona))
                    .Select(a => new
                    {
                        idPersona = a.IdPersona,
                        nombre = a.Nombre ?? "Sin nombre",
                        matricula = a.Matricula ?? "Sin matrícula",
                        email = a.Email ?? "",
                        foto = ProcesarFotoUniversal(a.Foto)
                    })
                    .OrderBy(a => a.nombre).ToList();

                // Limitar resultados si hay demasiados
                if (resultadoFinal.Count > 50)
                {
                    System.Diagnostics.Debug.WriteLine($"[ObtenerAlumnosNoVulnerables] Limitando de {resultadoFinal.Count} a 50 alumnos");
                    resultadoFinal = resultadoFinal.Take(50).ToList();
                }

                var jsonResult = new JsonResult
                {
                    Data = new
                    {
                        success = true,
                        alumnos = resultadoFinal,
                        total = resultadoFinal.Count
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    MaxJsonLength = Int32.MaxValue
                };
                return jsonResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR en ObtenerAlumnosNoVulnerables: {ex.ToString()}");
                return Json(new
                {
                    success = false,
                    message = "Error de conexión. Por favor, inténtelo de nuevo."
                }, JsonRequestBehavior.AllowGet);
            }
        }

        private IQueryable<DatosPersonales> QueryAlumnosDelGrupo(TutoriaGrupal g)
        {
            return db.DatosPersonales.Where(dp =>
                dp.IdCarrera == g.IdCarrera &&
                dp.IdGrado == g.IdGrado &&
                dp.IdGrupo == g.IdGrupo &&
                dp.IdTurno == g.IdTurno &&
                dp.IdPeriodo == g.IdPeriodo &&
                dp.Año == g.Año &&
                dp.Estado == true);
        }

        private List<EntrevistaInicial> UltimaEntrevistaPorAlumno(TutoriaGrupal g)
        {
            var idsAlumnosDelGrupo = QueryAlumnosDelGrupo(g).Select(dp => dp.IdPersona).Distinct().ToList();

            var ultimas = db.EntrevistaInicials
                .Where(ei => idsAlumnosDelGrupo.Contains(ei.IdPersona))
                .OrderByDescending(e => e.Fecha)
                .ThenByDescending(e => e.IdEntrevistaInicial)
                .ToList()
                .GroupBy(e => e.IdPersona)
                .Select(grp => grp.First())
                .ToList();

            return ultimas;
        }

        // Helper para obtener la PRIMERA entrevista del periodo (SNAPSHOT / FOTO INICIAL)
        private List<EntrevistaInicial> PrimeraEntrevistaPorAlumno(TutoriaGrupal g)
        {
            var idsAlumnosDelGrupo = QueryAlumnosDelGrupo(g).Select(dp => dp.IdPersona).Distinct().ToList();

            var primeras = db.EntrevistaInicials
                .Where(ei => idsAlumnosDelGrupo.Contains(ei.IdPersona))
                .OrderBy(e => e.Fecha) // Orden ASCENDENTE (De la más vieja a la más nueva)
                .ThenBy(e => e.IdEntrevistaInicial)
                .ToList()
                .GroupBy(e => e.IdPersona)
                .Select(grp => grp.First()) // Tomamos la primera
                .ToList();

            return primeras;
        }

        #endregion



        private string ProcesarFotoUniversal(string foto)
        {
            if (string.IsNullOrEmpty(foto))
                return "";

            try
            {
                // Limpiar prefijos data:image
                if (foto.StartsWith("data:image"))
                {
                    var index = foto.IndexOf(",");
                    if (index > 0)
                    {
                        return foto.Substring(index + 1);
                    }
                }

                // Si la foto es muy grande, intentar comprimirla o al menos devolverla
                // En lugar de omitirla completamente
                return foto;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcesarFotoUniversal] Error procesando foto: {ex.Message}");
                return "";
            }
        }

        [LecturaPermitida]
        [HttpPost]
        public ActionResult ObtenerTurnosPATs(string[] patIds)
        {
            try
            {
                if (patIds == null || patIds.Length == 0)
                {
                    return Json(new List<object>());
                }

                var patsConTurnos = new List<object>();

                foreach (var patIdStr in patIds)
                {
                    if (int.TryParse(patIdStr, out int patId))
                    {
                        var pat = db.PATs.Find(patId);
                        if (pat != null)
                        {
                            var tuto = db.TutoriaGrupals.FirstOrDefault(x => x.IdTutoriaGrupal == pat.IdTutoriaGrupal);
                            if (tuto != null)
                            {
                                string turnoNombre = "Sin turno";
                                string grupoNombre = "";

                                // Obtener información del turno
                                var turno = db.Turnoes.FirstOrDefault(a => a.IdTurno == tuto.IdTurno);
                                if (turno != null)
                                {
                                    turnoNombre = turno.Nombre;
                                }

                                // Generar nomenclatura del grupo usando la misma lógica que el PAT original
                                var carrera = db.Carreras.FirstOrDefault(a => a.IdCarrera == tuto.IdCarrera);
                                var grado = db.Gradoes.FirstOrDefault(a => a.IdGrado == tuto.IdGrado);
                                var grupo = db.Grupoes.FirstOrDefault(a => a.IdGrupo == tuto.IdGrupo);

                                if (turno != null)
                                {
                                    if (turno.Nombre == "Matutino")
                                    {
                                        grupoNombre += "M";
                                        turnoNombre = "Matutino";
                                    }
                                    else if (turno.Nombre == "Vespertino")
                                    {
                                        grupoNombre += "I";
                                        turnoNombre = "Vespertino";
                                    }
                                    else if (turno.Nombre == "Despresurizado")
                                    {
                                        grupoNombre += "D";
                                        turnoNombre = "Despresurizado";
                                    }
                                }

                                if (carrera != null)
                                {
                                    grupoNombre += carrera.Nomenclatura;
                                }
                                if (grado != null)
                                {
                                    grupoNombre += grado.Nombre;
                                }
                                if (grupo != null)
                                {
                                    grupoNombre += grupo.Nombre;
                                }

                                patsConTurnos.Add(new
                                {
                                    PATId = patId,
                                    Turno = turnoNombre,
                                    Grupo = grupoNombre
                                });
                            }
                        }
                    }
                }

                return Json(patsConTurnos);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener turnos de PATs: {ex.Message}");
                return Json(new List<object>());
            }
        }

        // =====================================================================
        // MÉTODOS AUXILIARES PARA CÁLCULO UNIFICADO DE VULNERABILIDADES
        // (Usando la misma lógica que el Tutor: tabla Seguimientos, primer seguimiento del periodo)
        // =====================================================================

        /// <summary>
        /// Calcula las vulnerabilidades usando la tabla Seguimientos (lógica correcta del Tutor).
        /// Toma el PRIMER seguimiento del periodo actual para cada alumno.
        /// </summary>
        private (int Economico, int Academico, int Personal) CalcularVulnerabilidadesPorSeguimiento(TutoriaGrupal tuto)
        {
            // Calcular fechas del periodo
            int ano = tuto.Año;
            int idPeriodo = tuto.IdPeriodo;
            DateTime fechaInicio, fechaFin;

            if (idPeriodo == 1) { fechaInicio = new DateTime(ano, 1, 1); fechaFin = new DateTime(ano, 4, 30); }
            else if (idPeriodo == 2) { fechaInicio = new DateTime(ano, 5, 1); fechaFin = new DateTime(ano, 8, 31); }
            else { fechaInicio = new DateTime(ano, 9, 1); fechaFin = new DateTime(ano, 12, 31); }

            // Obtener alumnos activos (sin bajas)
            var idsAlumnosActivos = QueryAlumnosDelGrupoSinBajas(tuto);

            // Consultar seguimientos del periodo actual
            var seguimientos = (from s in db.Seguimientoes
                                join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                where idsAlumnosActivos.Contains(i.IdPersona) &&
                                      s.Fecha >= fechaInicio && s.Fecha <= fechaFin
                                select new { i.IdPersona, s.Vulnerabilidad, s.Fecha, s.IdSeguimiento })
                               .ToList();

            // Tomar el PRIMER seguimiento de cada alumno (ordenado por fecha ascendente)
            var primerosSeguimientos = seguimientos
                .GroupBy(x => x.IdPersona)
                .Select(g => g.OrderBy(f => f.Fecha).ThenBy(f => f.IdSeguimiento).FirstOrDefault())
                .Where(x => x != null)
                .ToList();

            // Contar por tipo de vulnerabilidad (case-insensitive)
            int eco = primerosSeguimientos.Count(x => string.Equals(x.Vulnerabilidad, "Economico", StringComparison.OrdinalIgnoreCase));
            int aca = primerosSeguimientos.Count(x => string.Equals(x.Vulnerabilidad, "Academico", StringComparison.OrdinalIgnoreCase));
            int per = primerosSeguimientos.Count(x => string.Equals(x.Vulnerabilidad, "Personal", StringComparison.OrdinalIgnoreCase));

            return (eco, aca, per);
        }

        /// <summary>
        /// Obtiene los IDs de alumnos vulnerables de un tipo específico usando Seguimientos.
        /// </summary>
        private List<int> ObtenerIdsVulnerablesPorSeguimiento(TutoriaGrupal tuto, string tipoVulnerabilidad)
        {
            // Calcular fechas del periodo
            int ano = tuto.Año;
            int idPeriodo = tuto.IdPeriodo;
            DateTime fechaInicio, fechaFin;

            if (idPeriodo == 1) { fechaInicio = new DateTime(ano, 1, 1); fechaFin = new DateTime(ano, 4, 30); }
            else if (idPeriodo == 2) { fechaInicio = new DateTime(ano, 5, 1); fechaFin = new DateTime(ano, 8, 31); }
            else { fechaInicio = new DateTime(ano, 9, 1); fechaFin = new DateTime(ano, 12, 31); }

            // Obtener alumnos activos (sin bajas)
            var idsAlumnosActivos = QueryAlumnosDelGrupoSinBajas(tuto);

            // Consultar seguimientos del periodo actual
            var seguimientos = (from s in db.Seguimientoes
                                join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                where idsAlumnosActivos.Contains(i.IdPersona) &&
                                      s.Fecha >= fechaInicio && s.Fecha <= fechaFin
                                select new { i.IdPersona, s.Vulnerabilidad, s.Fecha, s.IdSeguimiento })
                               .ToList();

            // Tomar el PRIMER seguimiento de cada alumno (ordenado por fecha ascendente)
            var primerosSeguimientos = seguimientos
                .GroupBy(x => x.IdPersona)
                .Select(g => g.OrderBy(f => f.Fecha).ThenBy(f => f.IdSeguimiento).FirstOrDefault())
                .Where(x => x != null)
                .ToList();

            // Filtrar por tipo de vulnerabilidad (case-insensitive)
            return primerosSeguimientos
                .Where(x => string.Equals(x.Vulnerabilidad, tipoVulnerabilidad, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.IdPersona)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Cuenta los alumnos cuyo primer seguimiento tiene "No vulnerable" (igual que el Tutor).
        /// </summary>
        private int ContarNoVulnerablesPorSeguimiento(TutoriaGrupal tuto)
        {
            // Calcular fechas del periodo
            int ano = tuto.Año;
            int idPeriodo = tuto.IdPeriodo;
            DateTime fechaInicio, fechaFin;

            if (idPeriodo == 1) { fechaInicio = new DateTime(ano, 1, 1); fechaFin = new DateTime(ano, 4, 30); }
            else if (idPeriodo == 2) { fechaInicio = new DateTime(ano, 5, 1); fechaFin = new DateTime(ano, 8, 31); }
            else { fechaInicio = new DateTime(ano, 9, 1); fechaFin = new DateTime(ano, 12, 31); }

            // Obtener alumnos activos (sin bajas)
            var idsAlumnosActivos = QueryAlumnosDelGrupoSinBajas(tuto);

            // Consultar seguimientos del periodo actual
            var seguimientos = (from s in db.Seguimientoes
                                join i in db.Individuals on s.IdIndividual equals i.IdIndividual
                                where idsAlumnosActivos.Contains(i.IdPersona) &&
                                      s.Fecha >= fechaInicio && s.Fecha <= fechaFin
                                select new { i.IdPersona, s.Vulnerabilidad, s.Fecha, s.IdSeguimiento })
                               .ToList();

            // Tomar el PRIMER seguimiento de cada alumno (ordenado por fecha ascendente)
            var primerosSeguimientos = seguimientos
                .GroupBy(x => x.IdPersona)
                .Select(g => g.OrderBy(f => f.Fecha).ThenBy(f => f.IdSeguimiento).FirstOrDefault())
                .Where(x => x != null)
                .ToList();

            // Contar los que tienen "No vulnerable" (case-insensitive)
            return primerosSeguimientos.Count(x =>
                string.Equals(x.Vulnerabilidad, "No vulnerable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Vulnerabilidad, "NO VULNERABLE", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determina si una TutoriaGrupal corresponde a un cuatrimestre histórico
        /// (anterior al periodo/año actual del servidor).
        /// </summary>
        private bool EsPatHistorico(TutoriaGrupal tuto)
        {
            var tiempo = DateTime.Now;
            int periodoActual = (tiempo.Month >= 1 && tiempo.Month <= 4) ? 1 :
                                (tiempo.Month >= 5 && tiempo.Month <= 8) ? 2 : 3;
            int añoActual = tiempo.Year;
            return tuto.Año < añoActual || (tuto.Año == añoActual && tuto.IdPeriodo < periodoActual);
        }

        /// <summary>
        /// Construye la nomenclatura del grupo (ej. "TI6C") con el mismo formato con que
        /// se guarda en Individuals.Grupo: Carrera.Nomenclatura + Grado.Nombre + Grupo.Nombre.
        /// </summary>
        private string BuildGrupoNomenclatura(TutoriaGrupal tuto)
        {
            var car = db.Carreras.FirstOrDefault(c => c.IdCarrera == tuto.IdCarrera);
            var gra = db.Gradoes.FirstOrDefault(g => g.IdGrado == tuto.IdGrado);
            var grp = db.Grupoes.FirstOrDefault(g => g.IdGrupo == tuto.IdGrupo);
            return (car?.Nomenclatura ?? "") + (gra?.Nombre ?? "") + (grp?.Nombre ?? "");
        }

        /// <summary>
        /// Devuelve el texto del cuatrimestre tal como lo guarda Individuals.Cuatrimestre
        /// (formato "Enero - Abril", "Mayo - Agosto", "Septiembre - Diciembre").
        /// </summary>
        private string BuildCuatrimestreString(int idPeriodo)
        {
            if (idPeriodo == 1) return "Enero - Abril";
            if (idPeriodo == 2) return "Mayo - Agosto";
            return "Septiembre - Diciembre";
        }

        /// <summary>
        /// Para un PAT histórico, devuelve los IdPersona dados de baja DURANTE ese
        /// cuatrimestre concreto (filtrado por Grupo + rango de fechas del periodo).
        /// Excluye bajas posteriores en otros grupos, que no corresponden al PAT histórico.
        /// </summary>
        private List<int> ObtenerIdsBajasHistorico(TutoriaGrupal tuto)
        {
            string grupoNom = BuildGrupoNomenclatura(tuto);
            int ano = tuto.Año;
            DateTime fechaInicio, fechaFinExclusive;
            if (tuto.IdPeriodo == 1) { fechaInicio = new DateTime(ano, 1, 1); fechaFinExclusive = new DateTime(ano, 5, 1); }
            else if (tuto.IdPeriodo == 2) { fechaInicio = new DateTime(ano, 5, 1); fechaFinExclusive = new DateTime(ano, 9, 1); }
            else { fechaInicio = new DateTime(ano, 9, 1); fechaFinExclusive = new DateTime(ano + 1, 1, 1); }

            return db.Bajas
                .Where(b => b.Activo == true
                            && b.Grupo == grupoNom
                            && b.Fecha >= fechaInicio && b.Fecha < fechaFinExclusive)
                .Select(b => b.IdPersona)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Para un PAT histórico, devuelve los IdPersona de los alumnos que estuvieron en el
        /// grupo durante ese cuatrimestre, según el snapshot guardado en Individuals.
        /// </summary>
        private List<int> ObtenerIdsAlumnosHistorico(TutoriaGrupal tuto)
        {
            string grupoNom = BuildGrupoNomenclatura(tuto);
            string cuatri = BuildCuatrimestreString(tuto.IdPeriodo);
            int ano = tuto.Año;

            return db.Individuals
                .Where(i => i.Grupo == grupoNom
                            && i.Cuatrimestre == cuatri
                            && i.Fecha.Year == ano)
                .Select(i => i.IdPersona)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Para un PAT histórico: cruza Individuals con Seguimientoes (en el rango de fechas
        /// del periodo) y devuelve, por alumno, el PRIMER seguimiento (más antiguo) junto
        /// con su Vulnerabilidad.
        /// </summary>
        private List<HistVulnRow> ObtenerPrimerosSeguimientosHistorico(TutoriaGrupal tuto)
        {
            string grupoNom = BuildGrupoNomenclatura(tuto);
            string cuatri = BuildCuatrimestreString(tuto.IdPeriodo);
            int ano = tuto.Año;

            DateTime fechaInicio, fechaFin;
            if (tuto.IdPeriodo == 1) { fechaInicio = new DateTime(ano, 1, 1); fechaFin = new DateTime(ano, 4, 30); }
            else if (tuto.IdPeriodo == 2) { fechaInicio = new DateTime(ano, 5, 1); fechaFin = new DateTime(ano, 8, 31); }
            else { fechaInicio = new DateTime(ano, 9, 1); fechaFin = new DateTime(ano, 12, 31); }

            var seguimientos = (from i in db.Individuals
                                join s in db.Seguimientoes on i.IdIndividual equals s.IdIndividual
                                where i.Grupo == grupoNom
                                      && i.Cuatrimestre == cuatri
                                      && i.Fecha.Year == ano
                                      && s.Fecha >= fechaInicio && s.Fecha <= fechaFin
                                select new { i.IdPersona, s.Vulnerabilidad, s.Fecha, s.IdSeguimiento })
                               .ToList();

            return seguimientos
                .GroupBy(x => x.IdPersona)
                .Select(g => g.OrderBy(f => f.Fecha).ThenBy(f => f.IdSeguimiento).First())
                .Select(x => new HistVulnRow { IdPersona = x.IdPersona, Vulnerabilidad = x.Vulnerabilidad ?? "" })
                .ToList();
        }

        /// <summary>DTO interno para resultados históricos de vulnerabilidad por alumno.</summary>
        private class HistVulnRow
        {
            public int IdPersona { get; set; }
            public string Vulnerabilidad { get; set; }
        }

        /// <summary>DTO interno para datos básicos del alumno en el modal de vulnerables.</summary>
        private class AlumnoVulnRow
        {
            public int IdPersona { get; set; }
            public string Nombre { get; set; }
            public string Matricula { get; set; }
            public string Email { get; set; }
            public string Foto { get; set; }
        }

        /// <summary>
        /// Obtiene los IDs de alumnos del grupo excluyendo los que tienen baja activa.
        /// </summary>
        private List<int> QueryAlumnosDelGrupoSinBajas(TutoriaGrupal g)
        {
            var todosAlumnos = db.DatosPersonales.Where(dp =>
                dp.IdCarrera == g.IdCarrera && dp.IdGrado == g.IdGrado &&
                dp.IdGrupo == g.IdGrupo && dp.IdTurno == g.IdTurno &&
                dp.IdPeriodo == g.IdPeriodo && dp.Año == g.Año
            ).Select(x => x.IdPersona).ToList();

            var idsBajasActivas = db.Bajas
                .Where(b => todosAlumnos.Contains(b.IdPersona) && b.Activo == true)
                .Select(b => b.IdPersona)
                .ToList();

            return todosAlumnos.Except(idsBajasActivas).ToList();
        }

        private void SetListas()
        {
            //Listas de Carreras
            ViewBag.Careras = db.Carreras.ToList();
            //Listas de Administrativos
            //ViewBag.Administrativoes = db.Administrativoes.ToList();
        }
    }
}