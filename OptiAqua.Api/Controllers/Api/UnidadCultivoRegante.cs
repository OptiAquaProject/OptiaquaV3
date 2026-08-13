namespace WebApi {
    using DatosOptiaqua;
    using System;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Authorization;

    /// <summary>
    /// Proporciona las unidades de cultivo asociadas a un regante
    /// </summary>
    public class UnidadCultivoReganteController : ControllerBase {
        /// <summary>
        /// Unidades de cultivo asociadas a un regante en una temporada
        /// </summary>
        /// <param name="idRegante"></param>
        /// <param name="fecha"></param>
        /// <returns>The <see cref="IActionResult"/></returns>
        [Authorize]
        [Route("api/UnidadCultivoRegante/{idRegante}/{fecha}")]
        public IActionResult Get(int idRegante, string fecha) {
            try {                
                return Ok(DB.UnidadesCultivoList(idRegante, DateTime.Parse(fecha)));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Unidades de cultivo asociadas a un regante
        /// </summary>
        /// <param name="idRegante"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/UnidadCultivoRegante/{idRegante}")]
        public IActionResult Get(int idRegante) {
            try {
                return Ok(DB.UnidadCultivoList(idRegante));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }
    }
}
