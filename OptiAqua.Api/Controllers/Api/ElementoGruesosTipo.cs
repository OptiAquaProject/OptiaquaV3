namespace WebApi {
    using DatosOptiaqua;
    using System;
    using Microsoft.AspNetCore.Mvc;
    using webapi.Utiles;
    using Microsoft.AspNetCore.Authorization;

    /// <summary>
    /// Permite guardar y obtener la información relativa a los "Elementos gruesos tipo"
    /// </summary>
    public class ElementosGruesosTipoController : ControllerBase {
        /// <summary>
        /// Retorna los datos de la tabla "elementos gruesos tipo" para el elemento referenciado por IdElementosGruesosTipo
        /// </summary>
        /// <param name="IdElementosGruesosTipo"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/ElementosGruesosTipo/{IdElementosGruesosTipo}")]
        public IActionResult Get(string IdElementosGruesosTipo) {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.ElementosGruesosTipo(IdElementosGruesosTipo));
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Proporcina la lista de los "elemento gruesos tipo"
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [Route("api/ElementosGruesosTipo/")]
        public IActionResult GetList() {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.ElementosGruesosTipoList());
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }
    }
}
