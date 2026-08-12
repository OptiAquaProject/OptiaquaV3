namespace WebApi {
    using DatosOptiaqua;
    using Microsoft.AspNetCore.Mvc;
    using System;
    using webapi.Utiles;

    /// <summary>
    /// Página de inicio: cuadro de mando del sistema.
    /// </summary>
    public class HomeController : Controller {
        /// <summary>
        /// Cuadro de mando. Se construye siempre, aunque la base de datos no responda: en ese
        /// caso la página lo indica en lugar de devolver un error.
        /// </summary>
        public IActionResult Index() {
            ViewBag.Title = "OptiAqua — Cuadro de mando";
            DatosCuadroDeMando panel;
            try {
                panel = DatosCuadroDeMando.Recopila();
            } catch (Exception ex) {
                // Red de seguridad: ni siquiera un fallo inesperado al recopilar debe dejar la
                // página de inicio sin responder.
                Log.Error("No se pudo construir el cuadro de mando", ex);
                panel = new DatosCuadroDeMando {
                    BaseDatosOperativa = false,
                    ErrorBaseDatos = ex.Message
                };
            }
            return View(panel);
        }

        /// <summary>
        /// Progreso del recálculo en JSON, para que el cuadro de mando lo refresque sin
        /// recargar la página entera. Sólo devuelve progreso, ningún dato de negocio.
        /// </summary>
        [HttpGet]
        [Route("Home/ProgresoRecalculo")]
        public IActionResult ProgresoRecalculo() {
            return Ok(DatosOptiaqua.ProgresoRecalculo.Foto());
        }

        public IActionResult Test() {
            ViewBag.Title = "Página de test";
            ViewBag.nTemporadas = DB.TemporadasList()?.Count;
            ViewBag.nRiegosApiMes = DB.GetRiegosFromApi(DateTime.Today.AddDays(-30), DateTime.Today)?.Count;
            return View();
        }

    }
}
