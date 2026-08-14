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
                // La superficie entra en el balance hídrico: hay que tirar lo memorizado
                // de las unidades de cultivo que usan esta parcela.
                CacheDatosHidricos.SetDirtyParcela(idParcelaInt);
                TempData["ok"] = "Parcela " + idParcelaInt + " guardada.";
                return RedirectToAction("Parcelas");
            } catch (Exception ex) { Log.Error("Panel/ParcelaGuardar", ex); TempData["error"] = ex.Message; return RedirectToAction("ParcelaEditor", new { id = idParcelaInt }); }
        }

        // ===== Unidades de cultivo + último estado hídrico =====
        public IActionResult UnidadesCultivo(string buscar) {
            var lista = new List<DatosEstadoHidrico>();
            string idTemporada = null;
            try {
                idTemporada = DB.TemporadaActiva();
                var t = DB.Temporada(idTemporada);
                DateTime fecha = (t != null && t.FechaFinal < DateTime.Today) ? t.FechaFinal : DateTime.Today;
                List<string> lUC;
                using (var db = Conexion.Nueva()) {
                    // La búsqueda se resuelve en SQL y no sobre los estados ya calculados: si se
                    // filtrara después, buscar algo que está en la unidad 500 no lo encontraría
                    // nunca, porque a la pantalla solo llegan las 100 primeras.
                    var sql = @"SELECT DISTINCT uc.IdUnidadCultivo
                                FROM UnidadCultivoCultivo uc
                                LEFT JOIN Regante r ON r.IdRegante = uc.IdRegante
                                LEFT JOIN Cultivo  c ON c.IdCultivo = uc.IdCultivo
                                WHERE uc.IdTemporada = @0";
                    object[] args = new object[] { idTemporada };
                    if (!string.IsNullOrWhiteSpace(buscar)) {
                        sql += @" AND (uc.IdUnidadCultivo LIKE @1 OR r.Nombre LIKE @1 OR r.NIF LIKE @1
                                       OR c.Nombre LIKE @1
                                       OR EXISTS (SELECT 1 FROM ParcelasDeUC p
                                                  WHERE p.IdUnidadCultivo = uc.IdUnidadCultivo
                                                    AND p.IdTemporada = uc.IdTemporada
                                                    AND p.Descripcion LIKE @1))";
                        args = new object[] { idTemporada, "%" + buscar.Trim() + "%" };
                    }
                    sql += " ORDER BY uc.IdUnidadCultivo";
                    lUC = db.Fetch<string>(sql, args);
                }
                ViewBag.NTotal = lUC.Count;
                lista = EstadoHidricoMaterializado.ObtenerLista(idTemporada, lUC.Take(100), fecha);
            } catch (Exception ex) { Log.Error("Panel/UnidadesCultivo", ex); TempData["error"] = ex.Message; }
            ViewBag.Buscar = buscar;
            ViewBag.IdTemporada = idTemporada;
            return View(lista);
        }

        /// <summary>
        /// Ficha completa de una unidad de cultivo: de dónde salen sus datos y cómo ha ido la
        /// campaña. Cada bloque se rellena por separado, para que un fallo en uno —una unidad
        /// sin suelo, por ejemplo— no deje la pantalla en blanco.
        /// </summary>
        /// <param name="id">Identificador de la unidad de cultivo.</param>
        public IActionResult UnidadCultivo(string id) {
            if (string.IsNullOrWhiteSpace(id)) {
                TempData["error"] = "No se ha indicado la unidad de cultivo.";
                return RedirectToAction("UnidadesCultivo");
            }
            var detalle = new DetalleUnidadCultivo { IdUnidadCultivo = id };
            try {
                detalle.IdTemporada = DB.TemporadaActiva();
                var t = DB.Temporada(detalle.IdTemporada);
                detalle.Fecha = (t != null && t.FechaFinal < DateTime.Today) ? t.FechaFinal : DateTime.Today;

                try { detalle.Estado = EstadoHidricoMaterializado.Obtener(id, detalle.Fecha); }
                catch (Exception ex) { detalle.ErrorEstado = ex.Message; }

                try {
                    detalle.IdEstacion = DB.EstacionDeUC(id, detalle.IdTemporada);
                    detalle.EstacionNombre = DB.EstacionNombre(detalle.IdEstacion);
                } catch (Exception ex) { Log.Aviso("Panel/UnidadCultivo estación de " + id, ex); }

                try { detalle.Suelo.AddRange(DB.SueloUnidadCultivoTemporada(id, detalle.IdTemporada) ?? new List<DatosSuelo>()); }
                catch (Exception ex) { detalle.ErrorSuelo = ex.Message; }

                try { detalle.Etapas.AddRange(DB.UnidadCultivoCultivoEtapasList(id, detalle.IdTemporada) ?? new List<UnidadCultivoCultivoEtapas>()); }
                catch (Exception ex) { detalle.ErrorEtapas = ex.Message; }

                try { detalle.DatosExtra.AddRange(DB.DatosExtraList(id) ?? new List<UnidadCultivoDatosExtra>()); }
                catch (Exception ex) { detalle.ErrorDatosExtra = ex.Message; }

                try { detalle.Parcelas.AddRange(DB.GeoLocParcelasList(id, detalle.IdTemporada) ?? new List<GeoLocParcela>()); }
                catch (Exception ex) { detalle.ErrorParcelas = ex.Message; }

                // El balance da la evolución y las lluvias tal y como las ha visto el cálculo.
                DateTime siembra = detalle.Fecha, fin = detalle.Fecha;
                try {
                    var bh = BalanceHidrico.Balance(id, detalle.Fecha, false);
                    if (bh != null && bh.LineasBalance.Count > 0) {
                        detalle.Lineas.AddRange(bh.LineasBalance);
                        siembra = bh.unidadCultivoDatosHidricos.FechaSiembra();
                        fin = bh.LineasBalance[bh.LineasBalance.Count - 1].Fecha ?? detalle.Fecha;
                    }
                } catch (Exception ex) { detalle.ErrorBalance = ex.Message; }

                try { detalle.Riegos.AddRange(DB.RiegosList(id, siembra, fin) ?? new List<Riego>()); }
                catch (Exception ex) { detalle.ErrorRiegos = ex.Message; }

            } catch (Exception ex) {
                Log.Error("Panel/UnidadCultivo " + id, ex);
                TempData["error"] = ex.Message;
            }
            return View(detalle);
        }

        /// <summary>
        /// El balance de una unidad de cultivo en JSON, para el botón de copiar al
        /// portapapeles de su ficha.
        ///
        /// Va por aquí y no por `/api/balancehidrico` porque los endpoints de la API llevan
        /// `[Authorize]` a secas, que exige token JWT o clave de API; desde el navegador, con
        /// la cookie de sesión, responden 401. Este comparte la autenticación del panel.
        ///
        /// Tampoco se incrusta el JSON en la página: son 1,3 MB por unidad de cultivo, y
        /// cargarlos siempre para el caso en que alguien pulse copiar no sale a cuenta.
        /// </summary>
        /// <param name="id">Identificador de la unidad de cultivo.</param>
        public IActionResult BalanceJson(string id) {
            try {
                string idTemporada = DB.TemporadaActiva();
                var t = DB.Temporada(idTemporada);
                DateTime fecha = (t != null && t.FechaFinal < DateTime.Today) ? t.FechaFinal : DateTime.Today;
                var bh = BalanceHidrico.Balance(id, fecha, false);
                if (bh == null)
                    return NotFound(new { error = "No hay balance para " + id });
                return Json(bh.LineasBalance);
            } catch (Exception ex) {
                Log.Error("Panel/BalanceJson " + id, ex);
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Las parcelas de una unidad de cultivo en GeoJSON, para pintarlas en el mapa.
        /// Comparte la autenticación del panel, como el resto de la ficha.
        /// </summary>
        /// <param name="id">Identificador de la unidad de cultivo.</param>
        public IActionResult ParcelasGeoJson(string id) {
            try {
                string idTemporada = DB.TemporadaActiva();
                var parcelas = DB.GeoLocParcelasList(id, idTemporada) ?? new List<GeoLocParcela>();
                return Content(MapaParcelas.GeoJson(parcelas), "application/geo+json");
            } catch (Exception ex) {
                Log.Error("Panel/ParcelasGeoJson " + id, ex);
                return BadRequest(new { error = ex.Message });
            }
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
        public IActionResult EjecutarEstadoHidricoSql() {
            string texto = LeeScript("2026-08-14-estado-hidrico-uc.sql");
            if (texto == null) { TempData["error"] = "No se encontró el script sql/2026-08-14-estado-hidrico-uc.sql"; return RedirectToAction("Index", "Home"); }
            TempData["ok"] = DB.EjecutarScriptSql(texto);
            // Sin esto habría que reiniciar: la existencia de la tabla se comprueba una vez.
            DB.EstadoHidricoTablaOlvidaComprobacion();
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
            // El mismo trabajo que hace el planificador a las 9:00: clima, suelos y estados.
            Task.Run(() => { try { CacheDatosHidricos.PasadaDiaria(); } catch (Exception ex) { Log.Error("Panel/RecalculoTotal", ex); } });
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
