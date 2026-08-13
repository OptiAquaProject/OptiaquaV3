namespace WebApi {
    using DatosOptiaqua;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Models;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using webapi.Utiles;

    /// <summary>
    /// Panel de administración. Requiere sesión iniciada con rol de administrador (cookie).
    /// Ya no se piden usuario y contraseña en cada acción: basta estar identificado como admin.
    /// </summary>
    [Authorize(AuthenticationSchemes = "Cookies", Roles = "admin")]
    public class PanelController : Controller {
        private readonly IWebHostEnvironment entorno;
        public PanelController(IWebHostEnvironment entorno) { this.entorno = entorno; }

        // ===== Temporadas =====
        public IActionResult Temporadas() {
            return View(DB.TemporadasPanelList());
        }

        [HttpPost]
        public IActionResult TemporadaActivar(string idTemporada) {
            try {
                DB.TemporadaSetActiva(idTemporada);
                TempData["ok"] = "Temporada activa: " + idTemporada;
            } catch (Exception ex) { Log.Error("Panel/TemporadaActivar", ex); TempData["error"] = ex.Message; }
            return RedirectToAction("Temporadas");
        }

        // ===== Regantes =====
        public IActionResult Regantes(string buscar) {
            using (var db = Conexion.Nueva()) {
                var sql = "SELECT IdRegante, NIF, Nombre, Telefono, TelefonoSMS, Email, Role FROM Regante";
                object[] args = Array.Empty<object>();
                if (!string.IsNullOrWhiteSpace(buscar)) {
                    sql += " WHERE Nombre LIKE @0 OR NIF LIKE @0 OR CAST(IdRegante AS varchar(20)) = @1";
                    args = new object[] { "%" + buscar + "%", buscar };
                }
                sql += " ORDER BY Nombre";
                ViewBag.Buscar = buscar;
                return View(db.Fetch<Regante>(sql, args));
            }
        }

        public IActionResult ReganteEditor(int? id) {
            var regante = id == null ? new Regante { Role = "dbo" } : DB.Regante(id) as Regante;
            if (regante == null) { TempData["error"] = "No existe el regante " + id; return RedirectToAction("Regantes"); }
            return View(regante);
        }

        [HttpPost]
        public IActionResult ReganteGuardar(RegantePost regante) {
            try {
                var ret = DB.ReganteUpdate(regante);
                CacheDatosHidricos.SetDirtyContainsKey("/Regante");
                TempData["ok"] = "Regante guardado. " + ret;
                return RedirectToAction("Regantes");
            } catch (Exception ex) {
                Log.Error("Panel/ReganteGuardar", ex);
                TempData["error"] = ex.Message;
                return View("ReganteEditor", ARegante(regante));
            }
        }

        private static Regante ARegante(RegantePost r) => new Regante {
            IdRegante = r.IdRegante, NIF = r.NIF, Nombre = r.Nombre, Direccion = r.Direccion,
            CodigoPostal = r.CodigoPostal, Poblacion = r.Poblacion, Provincia = r.Provincia, Pais = r.Pais,
            Telefono = r.Telefono, TelefonoSMS = r.TelefonoSMS, Email = r.Email, Role = r.Role
        };

        // ===== Parcelas =====
        public IActionResult Parcelas(string buscar) {
            using (var db = Conexion.Nueva()) {
                var sql = "SELECT TOP 500 IdParcelaInt, IdRegante, Descripcion, SuperficieM2, IdMunicipio, IdPoligono, IdParcela, RefCatastral FROM Parcela";
                object[] args = Array.Empty<object>();
                if (!string.IsNullOrWhiteSpace(buscar)) {
                    sql += " WHERE Descripcion LIKE @0 OR RefCatastral LIKE @0 OR CAST(IdParcelaInt AS varchar(20)) = @1";
                    args = new object[] { "%" + buscar + "%", buscar };
                }
                sql += " ORDER BY IdParcelaInt";
                ViewBag.Buscar = buscar;
                return View(db.Fetch<ParcelaPoco>(sql, args));
            }
        }

        public IActionResult ParcelaEditor(int id) {
            var p = DB.Parcela(id);
            if (p == null) { TempData["error"] = "No existe la parcela " + id; return RedirectToAction("Parcelas"); }
            return View(p);
        }

        [HttpPost]
        public IActionResult ParcelaGuardar(int idParcelaInt, string descripcion, int? idRegante, double superficieM2) {
            try {
                DB.ParcelaGuardarDatos(idParcelaInt, descripcion, idRegante, superficieM2);
                TempData["ok"] = "Parcela " + idParcelaInt + " guardada.";
                return RedirectToAction("Parcelas");
            } catch (Exception ex) { Log.Error("Panel/ParcelaGuardar", ex); TempData["error"] = ex.Message; return RedirectToAction("ParcelaEditor", new { id = idParcelaInt }); }
        }

        // ===== Unidades de cultivo + último estado hídrico =====
        public IActionResult UnidadesCultivo() {
            var lista = new List<DatosEstadoHidrico>();
            string idTemporada = null;
            try {
                idTemporada = DB.TemporadaActiva();
                var t = DB.Temporada(idTemporada);
                DateTime fecha = (t != null && t.FechaFinal < DateTime.Today) ? t.FechaFinal : DateTime.Today;
                List<string> lUC;
                using (var db = Conexion.Nueva())
                    lUC = db.Fetch<string>("SELECT DISTINCT IdUnidadCultivo FROM UnidadCultivoCultivo WHERE IdTemporada=@0", idTemporada);
                ViewBag.NTotal = lUC.Count;
                foreach (var idUC in lUC.Take(100)) {
                    try {
                        var bh = BalanceHidrico.Balance(idUC, fecha);
                        if (bh != null) lista.Add(bh.DatosEstadoHidrico(fecha));
                    } catch (Exception ex) {
                        lista.Add(new DatosEstadoHidrico { IdUnidadCultivo = idUC, IdTemporada = idTemporada, Status = "ERROR: " + ex.Message });
                    }
                }
            } catch (Exception ex) { Log.Error("Panel/UnidadesCultivo", ex); TempData["error"] = ex.Message; }
            ViewBag.IdTemporada = idTemporada;
            return View(lista);
        }

        // ===== Eventos con filtro por fechas =====
        public IActionResult Eventos(DateTime? desde, DateTime? hasta, string texto) {
            ViewBag.Desde = desde; ViewBag.Hasta = hasta; ViewBag.Texto = texto;
            ViewBag.HayColumnaFecha = DB.EventosTieneColumnaFecha();
            return View(DB.EventosList(desde, hasta, texto));
        }

        // ===== Acciones de administración =====
        [HttpPost]
        public IActionResult EjecutarApiKeySql() {
            string texto = LeeScript("2026-08-12-apikey.sql");
            if (texto == null) { TempData["error"] = "No se encontró el script sql/2026-08-12-apikey.sql"; return RedirectToAction("Index", "Home"); }
            TempData["ok"] = DB.EjecutarScriptSql(texto);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public IActionResult RefrescarSiar() {
            Task.Run(() => { try { DB.DatosClimaticosSiarForceRefresh(); } catch (Exception ex) { Log.Error("Panel/RefrescarSiar", ex); } });
            TempData["ok"] = "Actualización del SIAR lanzada. Puede tardar; revisa los eventos y el cuadro de mando.";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public IActionResult RecalculoTotal() {
            if (CacheDatosHidricos.Recalculando) { TempData["error"] = "Ya hay un recálculo en curso."; return RedirectToAction("Index", "Home"); }
            // Se lanza en el propio proceso web para que el panel de progreso lo muestre.
            Task.Run(() => { try { CacheDatosHidricos.RecreateAll(); } catch (Exception ex) { Log.Error("Panel/RecalculoTotal", ex); } });
            TempData["ok"] = "Recálculo total lanzado. Sigue el progreso en el cuadro de mando.";
            return RedirectToAction("Index", "Home");
        }

        private string LeeScript(string nombre) {
            var candidatos = new[] {
                Path.Combine(entorno.ContentRootPath, "..", "sql", nombre),
                Path.Combine(entorno.ContentRootPath, "sql", nombre),
                Path.Combine(AppContext.BaseDirectory, "sql", nombre),
            };
            foreach (var ruta in candidatos) {
                try { if (System.IO.File.Exists(ruta)) return System.IO.File.ReadAllText(ruta); } catch { }
            }
            return null;
        }
    }
}
