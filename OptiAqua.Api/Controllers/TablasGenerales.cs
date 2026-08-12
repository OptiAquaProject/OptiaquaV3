namespace WebApi {
    using DatosOptiaqua;
    using System;
    using Microsoft.AspNetCore.Mvc;
    using webapi.Utiles;
    using Microsoft.AspNetCore.Authorization;

    /// <summary>
    /// Proporciona información sobre las temporadas
    /// </summary>
    public class TablasMaestrasController : ControllerBase {
        /// <summary>
        ///  Parajes
        /// </summary>
        /// <returns>The <see cref="IActionResult"/></returns>
        [HttpGet]
        [Authorize]
        [Route("api/Parajes/")]
        public IActionResult Parajes() {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.ParajesList());
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        ///  Municipios
        /// </summary>
        /// <returns>The <see cref="IActionResult"/></returns>
        [HttpGet]
        [Authorize]
        [Route("api/Municipios/")]
        public IActionResult Municipios() {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.MunicipiosList());
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        ///  Provincias
        /// </summary>
        /// <returns>The <see cref="IActionResult"/></returns>
        [HttpGet]
        [Authorize]
        [Route("api/Provincias/")]
        public IActionResult Provincias() {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.ProvinciaList());
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        ///  Cultivos
        /// </summary>
        /// <returns>The <see cref="IActionResult"/></returns>
        [HttpGet]
        [Authorize]
        [Route("api/Cultivos/")]
        public IActionResult Cultivos() {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.CultivosList());
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }
    }
}
