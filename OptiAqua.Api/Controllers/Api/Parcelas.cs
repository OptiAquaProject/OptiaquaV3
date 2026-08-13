namespace WebApi {
    using DatosOptiaqua;
    using System;
    using System.Security.Claims;
    
    using Microsoft.AspNetCore.Mvc;
    using webapi.Utiles;
    using Microsoft.AspNetCore.Authorization;

    /// <summary>
    /// Proporciona los datos de las parcelas y las propiedades de su suelo.
    /// </summary>
    public class ParcelaController : ControllerBase {
        /// <summary>
        /// Datos de la parcela indicada
        /// </summary>
        /// <param name="idParcela"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/Parcela/{idParcela}")]
        public IActionResult Get(int idParcela) {
            try {
                int idUsuario; string role;
                if (!User.TryLeer(out idUsuario, out role))
                    return Unauthorized();
                // La comprobación de acceso queda fuera de la caché: dentro, se memorizaba la
                // propia respuesta 401 bajo la clave de la petición.
                if (!DB.EstaAutorizado(idUsuario, role, idParcela))
                    return Unauthorized();
                return Ok(CacheDatosHidricos.Cache(Request.Path.ToString() + "Usuario" + idUsuario.ToString(),
                    () => DB.Parcela(idParcela)));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Lista de parcelas de una unidad de cultivo en una temporada
        /// </summary>
        /// <param name="fecha"></param>
        /// <param name="idUnidadCultivo"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/ParcelasDeUnidadDeCultivo/{IdUnidadCultivo}/{Fecha}")]
        public IActionResult GetParcelasDeUnidadDeCultivo(string fecha, string idUnidadCultivo) {
            try {
                DateTime dFecha = DateTime.Parse(fecha);
                int idUsuario; string role;
                if (!User.TryLeer(out idUsuario, out role))
                    return Unauthorized();

                var idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, dFecha);
                if (!DB.EstaAutorizado(idUsuario, role, idUnidadCultivo, idTemporada))
                    return Unauthorized();
                return Ok(CacheDatosHidricos.Cache(Request.Path.ToString() + "Usuario" + idUsuario.ToString(),
                    () => DB.IdParcelasList(idUnidadCultivo, idTemporada)));

            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Listado de todas las parcelas
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [Route("api/parcelas/")]
        public IActionResult GetParcelas() {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.ParcelasList());
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Lista con datos ampliados de las parcelas con filtros.
        /// </summary>
        /// <param name="Fecha"></param>
        /// <param name="IdParcela"></param>
        /// <param name="IdRegante"></param>
        /// <param name="IdMunicipio"></param>
        /// <param name="Search"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/ParcelaList/{IdTemporada}/{IdParcela}/{IdRegante}/{IdMunicipio}/{Search}")]
        public IActionResult GetParcelaList(string Fecha, string IdParcela, string IdRegante, string IdMunicipio, string Search) {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.ParcelaList(Fecha, IdParcela, IdRegante, IdMunicipio, Search));
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }
    }
}
