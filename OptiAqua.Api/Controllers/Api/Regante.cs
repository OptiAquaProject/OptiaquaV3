namespace WebApi {
    using DatosOptiaqua;
    using Models;
    using System;
    using System.Security.Claims;
    
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Authorization;
    using webapi.Utiles;

    /// <summary>
    /// Proporciona los datos de las parcelas y las propiedades de su suelo.
    /// </summary>
    public class ReganteController : ControllerBase {
        /// <summary>
        /// Datos del regante indicado
        /// </summary>
        /// <param name="idRegante"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/Regante/{idRegante}")]
        public IActionResult Get(int idRegante) {
            try {
                int IdUsuario; string rolUsuario;
                if (!User.TryLeer(out IdUsuario, out rolUsuario)) return Unauthorized();
                bool isAdmin = User.EsAdmin();
                if (isAdmin == false && IdUsuario != idRegante) {
                    return BadRequest("La parcela no pertenece al regante");
                }
                return Ok(CacheDatosHidricos.Cache(Request.Path.ToString()+"Usuario"+IdUsuario.ToString(), () => DB.Regante(idRegante)));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Lista los regantes
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [Route("api/Regantes")]
        public IActionResult Get() {
            try {
                int IdUsuario; string rolUsuario;
                if (!User.TryLeer(out IdUsuario, out rolUsuario)) return Unauthorized();
                bool isAdmin = User.EsAdmin();
                if (isAdmin == false) {
                    return BadRequest("La parcela no pertenece al regante");
                }
                return Ok(CacheDatosHidricos.Cache(Request.Path.ToString() + "Usuario" + IdUsuario.ToString(), () => DB.RegantesList()));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Lista datos ampliados de regantes con filtros
        /// </summary>
        /// <param name="Fecha"></param>
        /// <param name="IdRegante"></param>
        /// <param name="IdUnidadCultivo"></param>
        /// <param name="IdParcela"></param>
        /// <param name="Search"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/ReganteList/{Fecha}/{IdRegante}/{IdUnidadCultivo}/{IdParcela}/{Search}")]
        public IActionResult GetReganteList(string Fecha, string IdRegante, string IdUnidadCultivo, string IdParcela, string Search) {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString() , () => {
                    return Ok(DB.ReganteList(Fecha, IdRegante, IdUnidadCultivo, IdParcela, Search));
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Actualización de los datos del Regante
        /// </summary>
        /// <param name="regante"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost]
        [Route("api/ReganteUpdate")]
        public IActionResult ReganteUpdate([FromBody] RegantePost regante) {
            try {
                // El cuerpo de la petición incluye el campo Role, que se persiste tal cual:
                // sin esta comprobación cualquier regante autenticado podía darse a sí mismo
                // role de administrador, o reescribir la ficha de otro regante.
                if (!User.EsAdmin())
                    return Unauthorized();
                if (regante == null)
                    return BadRequest("No se han recibido datos del regante");

                // Una sola llamada: antes se invocaba dos veces, duplicando la escritura.
                string ret = DB.ReganteUpdate(regante);
                CacheDatosHidricos.SetDirtyContainsKey("/Regante");
                return Ok(ret);
            } catch (Exception ex) {
                Log.Error("api/ReganteUpdate - IdRegante:" + (regante == null ? "(nulo)" : regante.IdRegante.ToString()), ex);
                return BadRequest(ex.Message);
            }
        }
    }
}
