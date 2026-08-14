namespace WebApi {
    using DatosOptiaqua;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using webapi.Utiles;

    /// <summary>
    /// LAB-ONE: el laboratorio del balance hídrico.
    ///
    /// Se coge una unidad de cultivo real, se copia entera a memoria y a partir de ahí se puede
    /// cambiar cualquier dato —el suelo, el clima, las etapas, los riegos, la superficie— y ver
    /// qué sale. Nada de lo que se hace aquí llega a la base de datos: es para entender el
    /// modelo y para probar hipótesis, no para corregir datos de producción.
    ///
    /// El ensayo vive en memoria mientras se trabaja; para conservarlo hay que guardarlo en un
    /// JSON, que es también la forma de pasárselo a otro.
    /// </summary>
    [Authorize(AuthenticationSchemes = "Cookies", Roles = "admin")]
    public class LabOneController : Controller {

        public LabOneController(IWebHostEnvironment entorno) {
            // Los ensayos se guardan fuera de wwwroot: no tienen por qué servirse por HTTP.
            if (string.IsNullOrEmpty(LabOneAlmacen.Carpeta))
                LabOneAlmacen.Carpeta = Path.Combine(entorno.ContentRootPath, "App_Data", "LabOne");
        }

        /// <summary>Quién tiene abierto el ensayo. Uno por usuario, para que nadie se pise.</summary>
        private string Usuario => User?.Identity?.Name ?? "";

        // ===== Entrada =====

        /// <summary>
        /// Pide la unidad de cultivo y la temporada con la que empezar, y enseña lo que ya haya:
        /// el ensayo abierto y los guardados en disco.
        /// </summary>
        public IActionResult Index() {
            ViewBag.Abierto = LabOneAlmacen.Abierto(Usuario);
            ViewBag.Guardados = LabOneAlmacen.Guardados();
            ViewBag.Temporadas = DB.TemporadasList() ?? new List<Models.Temporada>();
            ViewBag.TemporadaActiva = DB.TemporadaActiva();
            return View();
        }

        /// <summary>
        /// Copia una unidad de cultivo a un ensayo nuevo y lo abre.
        /// </summary>
        /// <param name="idUnidadCultivo">Unidad de cultivo de la que partir.</param>
        /// <param name="idTemporada">Temporada que se quiere ensayar.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Nuevo(string idUnidadCultivo, string idTemporada) {
            try {
                if (string.IsNullOrWhiteSpace(idUnidadCultivo))
                    throw new Exception("Hay que indicar la unidad de cultivo.");
                if (string.IsNullOrWhiteSpace(idTemporada))
                    idTemporada = DB.TemporadaActiva();

                var t = DB.Temporada(idTemporada);
                if (t == null)
                    throw new Exception("No existe la temporada " + idTemporada);

                // La fecha manda: es la que decide qué temporada carga el motor. Se toma el
                // último día con sentido de la temporada pedida, igual que en la ficha real.
                DateTime fecha = t.FechaFinal < DateTime.Today ? t.FechaFinal : DateTime.Today;
                if (fecha < t.FechaInicial) fecha = t.FechaFinal;

                var ensayo = LabOneEnsayo.Cargar(idUnidadCultivo.Trim(), fecha);
                LabOneAlmacen.Abre(Usuario, ensayo);
                TempData["ok"] = $"Ensayo cargado desde {ensayo.IdUnidadCultivo} ({ensayo.IdTemporada}).";
                return RedirectToAction("Ficha");
            } catch (Exception ex) {
                Log.Error("LabOne/Nuevo " + idUnidadCultivo, ex);
                TempData["error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ===== El ensayo =====

        /// <summary>El ensayo abierto, con sus datos editables y el resultado de calcularlos.</summary>
        public IActionResult Ficha() {
            var ensayo = LabOneAlmacen.Abierto(Usuario);
            if (ensayo == null) {
                TempData["error"] = "No hay ningún ensayo abierto.";
                return RedirectToAction("Index");
            }
            return View(Calcula(ensayo));
        }

        /// <summary>
        /// Recibe el ensayo entero, lo deja en memoria y vuelve a calcular.
        ///
        /// Viaja como UN campo con el ensayo en JSON, y no como trescientos campos sueltos, por
        /// dos motivos. El primero es el tope de 1.024 claves por formulario: la serie climática
        /// son más de trescientos días con cinco valores cada uno, y pasado el tope el modelo
        /// llegaría cortado sin avisar. El segundo es el separador decimal: el navegador envía
        /// los campos numéricos con punto y el enlace de modelos los interpreta con la cultura
        /// del servidor, que aquí es española; "2500.5" se convertiría en 25005 en silencio. En
        /// JSON los números son siempre invariantes y ese problema no existe.
        /// </summary>
        /// <param name="json">El ensayo completo en JSON.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Recalcular(string json) {
            var abierto = LabOneAlmacen.Abierto(Usuario);
            if (abierto == null) {
                TempData["error"] = "No hay ningún ensayo abierto.";
                return RedirectToAction("Index");
            }
            var ensayo = abierto;
            try {
                var recibido = LabOneAlmacen.DeSerializado(json);
                recibido.Creado = abierto.Creado;
                LabOneAlmacen.Abre(Usuario, recibido);
                ensayo = recibido;
            } catch (Exception ex) {
                Log.Error("LabOne/Recalcular", ex);
                TempData["error"] = "No se ha podido leer el ensayo: " + ex.Message;
            }
            return View("Ficha", Calcula(ensayo));
        }

        /// <summary>
        /// Vuelve a copiar los datos de la base de datos, tirando los cambios del ensayo.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Restaurar() {
            var abierto = LabOneAlmacen.Abierto(Usuario);
            if (abierto == null) return RedirectToAction("Index");
            try {
                var nuevo = LabOneEnsayo.Cargar(abierto.IdUnidadCultivo, abierto.Fecha);
                nuevo.Nombre = abierto.Nombre;
                nuevo.Notas = abierto.Notas;
                LabOneAlmacen.Abre(Usuario, nuevo);
                TempData["ok"] = "Datos vueltos a copiar de la base de datos.";
            } catch (Exception ex) {
                Log.Error("LabOne/Restaurar", ex);
                TempData["error"] = ex.Message;
            }
            return RedirectToAction("Ficha");
        }

        /// <summary>Cierra el ensayo abierto. Lo que no se haya guardado en disco se pierde.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cerrar() {
            LabOneAlmacen.Cierra(Usuario);
            return RedirectToAction("Index");
        }

        // ===== Disco =====

        /// <summary>
        /// Guarda en un JSON el ensayo tal y como está en pantalla.
        ///
        /// Llega el ensayo completo, no solo el nombre: el botón de guardar comparte formulario
        /// con el de recalcular, y lo que hay que conservar es lo que se está viendo, no lo
        /// último que se calculó.
        /// </summary>
        /// <param name="json">El ensayo completo en JSON.</param>
        /// <param name="nombre">Nombre con el que guardarlo.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GuardarDisco(string json, string nombre) {
            var ensayo = LabOneAlmacen.Abierto(Usuario);
            if (ensayo == null) return RedirectToAction("Index");
            try {
                if (!string.IsNullOrWhiteSpace(json)) {
                    var recibido = LabOneAlmacen.DeSerializado(json);
                    recibido.Creado = ensayo.Creado;
                    ensayo = recibido;
                }
                if (!string.IsNullOrWhiteSpace(nombre)) ensayo.Nombre = nombre.Trim();
                LabOneAlmacen.Abre(Usuario, ensayo);
                string fichero = LabOneAlmacen.GuardaEnDisco(ensayo);
                TempData["ok"] = "Ensayo guardado como " + fichero;
            } catch (Exception ex) {
                Log.Error("LabOne/GuardarDisco", ex);
                TempData["error"] = ex.Message;
            }
            return RedirectToAction("Ficha");
        }

        /// <summary>Abre un ensayo guardado en disco.</summary>
        /// <param name="fichero">Nombre del fichero dentro de la carpeta de ensayos.</param>
        public IActionResult CargarDisco(string fichero) {
            try {
                LabOneAlmacen.Abre(Usuario, LabOneAlmacen.LeeDeDisco(fichero));
                return RedirectToAction("Ficha");
            } catch (Exception ex) {
                Log.Error("LabOne/CargarDisco " + fichero, ex);
                TempData["error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        /// <summary>Borra un ensayo guardado.</summary>
        /// <param name="fichero">Nombre del fichero dentro de la carpeta de ensayos.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BorrarDisco(string fichero) {
            try {
                LabOneAlmacen.BorraDeDisco(fichero);
                TempData["ok"] = "Ensayo borrado.";
            } catch (Exception ex) {
                Log.Error("LabOne/BorrarDisco " + fichero, ex);
                TempData["error"] = ex.Message;
            }
            return RedirectToAction("Index");
        }

        /// <summary>El ensayo abierto en JSON, para descargarlo o copiarlo.</summary>
        public IActionResult Json() {
            var ensayo = LabOneAlmacen.Abierto(Usuario);
            if (ensayo == null) return NotFound(new { error = "No hay ningún ensayo abierto." });
            return Content(LabOneAlmacen.ASerializado(ensayo), "application/json", Encoding.UTF8);
        }

        /// <summary>El balance del ensayo en JSON, para comparar con el de producción.</summary>
        public IActionResult BalanceJson() {
            var ensayo = LabOneAlmacen.Abierto(Usuario);
            if (ensayo == null) return NotFound(new { error = "No hay ningún ensayo abierto." });
            try {
                var dh = new UnidadCultivoDatosHidricos(ensayo);
                var bh = new BalanceHidrico(dh, false, dh.FechaFinalDeEstudio());
                return Json(bh.LineasBalance);
            } catch (Exception ex) {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ===== Cálculo =====

        /// <summary>
        /// Calcula el balance del ensayo. Siempre con actualizaEtapas en false: es la única
        /// escritura que hace el motor y en un ensayo no tiene ningún sentido que las fechas
        /// inventadas aquí acaben en la tabla de producción.
        /// </summary>
        /// <param name="ensayo">Ensayo a calcular.</param>
        private LabOneFicha Calcula(LabOneEnsayo ensayo) {
            var ret = new LabOneFicha { Ensayo = ensayo };
            try { ret.TiposEstres = DB.ListaTipoEstres().Keys.OrderBy(x => x).ToList(); }
            catch (Exception ex) { Log.Aviso("LabOne tipos de estrés", ex); }

            var reloj = Stopwatch.StartNew();
            try {
                var dh = new UnidadCultivoDatosHidricos(ensayo);
                var bh = new BalanceHidrico(dh, false, dh.FechaFinalDeEstudio());
                ret.Lineas = bh.LineasBalance;
                ret.Estado = bh.DatosEstadoHidrico(ensayo.Fecha);
                ret.Incidencias = dh.Incidencias.Resumen();
            } catch (Exception ex) {
                ret.ErrorCalculo = ex.Message;
                ret.Incidencias.Add(new Incidencia {
                    Codigo = "CALCULO_FALLIDO",
                    Gravedad = GravedadIncidencia.Error,
                    Mensaje = ex.Message
                });
            }
            reloj.Stop();
            ret.Milisegundos = reloj.ElapsedMilliseconds;
            return ret;
        }
    }
}
