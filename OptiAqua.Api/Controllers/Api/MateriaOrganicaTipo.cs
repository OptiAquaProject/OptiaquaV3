namespace WebApi {
    using DatosOptiaqua;
    using System;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Authorization;

    /// <summary>
    /// Permite guardar y obtener la información relativa a la "materia orgánica tipo"
    /// </summary>
    public class MateriaOrganicaTipoController : ControllerBase {
        /// <summary>
        /// Proporcina de los valores de la tabla "materia orgánica tipo" para el resgistro referenciado por idMateriaOrganicatipo
        /// </summary>
        /// <param name="idMateriaOrganicatipo"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/MateriaOrganicaTipo/{idMateriaOrganicatipo}")]
        public IActionResult Get(string idMateriaOrganicatipo) {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.MateriaOrganicaTipo(idMateriaOrganicatipo));
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Proporcina la lista de las materias orgánicas tipo.
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [Route("api/MateriaOrganicaTipo/")]
        public IActionResult GetListSuelos() {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.MateriaOrganicaTipoList());
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }
    }
}
