namespace WebApi {
    using DatosOptiaqua;
    using Models;
    using System;
    using System.Linq;
    using System.Security.Claims;
    
    using Microsoft.AspNetCore.Mvc;
    using webapi.Utiles;
    using Microsoft.AspNetCore.Authorization;

    /// <summary>
    /// Proporciona información sobre las temporadas
    /// </summary>
    public class TemporadasController : ControllerBase {
        /// <summary>
        /// Todas las temporadas definidas.
        /// </summary>
        /// <returns></returns>
        [Authorize]
        public IActionResult Get() {
            try {
                return Ok(DB.TemporadasList());
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Retorna los datos de una temporada
        /// </summary>
        /// <param name="idTemporada"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/temporada/{idTemporada}")]
        public IActionResult GetTemporada(string idTemporada) {
            try {
                return Ok(DB.Temporada(idTemporada));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Todas las temporadas de la unidad de cultivo indicada.
        /// </summary>
        /// <param name="idUnidadCultivo"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/temporadas/{idUnidadCultivo}")]
        public IActionResult Get(string idUnidadCultivo) {
            try {
                /*
                int idRegante; string rolUsuario;
                if (!User.TryLeer(out idRegante, out rolUsuario)) return Unauthorized();
                bool isAdmin = User.EsAdmin();
                if (isAdmin == false && DB.LaUnidadDeCultivoPerteneceAlRegante(idUnidadCultivo, idRegante) == false) {
                    return BadRequest("La Unidad de cultivo no pertenece al regante");
                }
                */
                return Ok(DB.TemporadasUnidadCultivoList(idUnidadCultivo));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        ///  PostUnidadCultivoTemporadaCosteM3Agua
        /// </summary>
        /// <param name="temporada">The temporada<see cref="Temporada"/></param>
        /// <returns>The <see cref="IActionResult"/></returns>
        [Authorize]
        [HttpPost]
        [Route("api/Temporada/")]
        public IActionResult PostUnidadCultivoTemporadaCosteM3Agua([FromBody] Temporada temporada) {
            try {
                /*
                bool isAdmin = User.EsAdmin();
                if (isAdmin == false)
                    return Unauthorized();
                */
                CacheDatosHidricos.ClearAll();
                return Ok(DB.TemporadaSave(temporada));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }
    }
}
