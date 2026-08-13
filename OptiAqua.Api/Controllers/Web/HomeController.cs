namespace WebApi {
    using DatosOptiaqua;
    using Microsoft.AspNetCore.Mvc;
    using Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using webapi.Utiles;

    /// <summary>
    /// Página de inicio. Se adapta al rol:
    ///  - sin identificar: información general y login;
    ///  - admin: cuadro de mando completo con el panel de administración;
    ///  - dbo (usuario) y asesor (gestor): sólo sus unidades de cultivo, en modo consulta.
    /// </summary>
    public class HomeController : Controller {
        public IActionResult Index() {
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return View("Bienvenida");

            if (User.IsInRole("admin")) {
                ViewBag.Title = "OptiAqua — Cuadro de mando";
                DatosCuadroDeMando panel;
                try {
                    panel = DatosCuadroDeMando.Recopila();
                } catch (Exception ex) {
                    // Red de seguridad: ni un fallo inesperado debe dejar la página sin responder.
                    Log.Error("No se pudo construir el cuadro de mando", ex);
                    panel = new DatosCuadroDeMando { BaseDatosOperativa = false, ErrorBaseDatos = ex.Message };
                }
                return View(panel);
            }

            // Usuario o gestor -> su zona de consulta.
            return RedirectToAction("MiZona");
        }

        /// <summary>
        /// Zona de consulta para usuario (sus unidades) y gestor (las de sus representados).
        /// Sólo lectura: no puede modificar datos comunes.
        /// </summary>
        [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "Cookies")]
        public IActionResult MiZona() {
            var lista = new List<DatosEstadoHidrico>();
            string idTemporada = null;
            int idUsuario; string role;
            if (!User.TryLeer(out idUsuario, out role))
                return RedirectToAction("Login", "Cuenta");
            try {
                idTemporada = DB.TemporadaActiva();
                var t = DB.Temporada(idTemporada);
                DateTime fecha = (t != null && t.FechaFinal < DateTime.Today) ? t.FechaFinal : DateTime.Today;
                List<string> lUC = DB.UnidadesDeUsuario(idUsuario, role, idTemporada);
                ViewBag.NTotal = lUC.Count;
                lista = EstadoHidricoMaterializado.ObtenerLista(idTemporada, lUC.Take(200), fecha);
            } catch (Exception ex) { Log.Error("Home/MiZona", ex); ViewBag.Error = ex.Message; }
            ViewBag.IdTemporada = idTemporada;
            ViewBag.EsGestor = role == "asesor";
            return View(lista);
        }

        /// <summary>
        /// Progreso del recálculo en JSON, para refrescar el cuadro de mando sin recargar.
        /// </summary>
        [HttpGet]
        [Route("Home/ProgresoRecalculo")]
        public IActionResult ProgresoRecalculo() {
            return Ok(DatosOptiaqua.ProgresoRecalculo.Foto());
        }
    }
}
