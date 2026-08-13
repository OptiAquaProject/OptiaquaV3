namespace WebApi {
    using DatosOptiaqua;
    using System;
    using System.Linq;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Authorization;
    using webapi.Utiles;

    /// <summary>
    /// Proporciona información del balance hídrico
    /// </summary>
    public class BalanceHidricoController : ControllerBase {
        /// <summary>
        /// Balance hídrico de una unidad de cultivo en una temporada.
        /// </summary>
        /// <param name="idUnidadCultivo">Identificador de la unidad de cultivo</param>
        /// <param name="fecha">Identificador de la temporada</param>
        /// <param name="actualizaFechasEtapas">Activar si se desea recalcular las fechas de las etapas para la parcela indicada</param>
        /// <returns></returns>
        [Authorize]
        [Route("api/balancehidrico/{idUnidadCultivo}/{fecha}/{actualizaFechasEtapas}")]
        public IActionResult GetBalanceHidrico(string idUnidadCultivo, string fecha, bool actualizaFechasEtapas) {
            try {
                DateTime dFecha = DateTime.Parse(fecha);
                int idUsuario;
                string role;
                if (!User.TryLeer(out idUsuario, out role))
                    return Unauthorized();
                string idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, dFecha);
                if (!DB.EstaAutorizado(idUsuario, role, idUnidadCultivo, idTemporada))
                    return Unauthorized();

                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    BalanceHidrico bh = BalanceHidrico.Balance(idUnidadCultivo, dFecha, actualizaFechasEtapas);
                    var ret = Ok(bh.LineasBalance);
                    return ret;
                });

            } catch (Exception ex) {
                Log.Error("api/balancehidrico - UC:" + idUnidadCultivo + " fecha:" + fecha, ex);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Retorna resumen de los datos hídricos a una fecha.
        /// </summary>
        /// <param name="idUnidadCultivo"></param>
        /// <param name="fecha"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/DatosHidricos/{idUnidadCultivo}/{fecha}")]
        public IActionResult GetDatosHidricos(string idUnidadCultivo, string fecha) {
            try {
                DateTime dFecha = DateTime.Parse(fecha);
                int idUsuario;
                string role;
                if (!User.TryLeer(out idUsuario, out role))
                    return Unauthorized();
                string idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, dFecha);
                if (!DB.EstaAutorizado(idUsuario, role, idUnidadCultivo, idTemporada))
                    return Unauthorized();
                return CacheDatosHidricos.Cache(Request.Path.ToString() + "Usuario" + idUsuario.ToString(),
                    () => Ok(EstadoHidricoMaterializado.Obtener(idUnidadCultivo, dFecha)));

            } catch (Exception ex) {
                Log.Error("api/DatosHidricos - UC:" + idUnidadCultivo + " fecha:" + fecha, ex);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Listado de los balances hídricos
        /// </summary>
        /// <param name="idRegante"></param>
        /// <param name="idUnidadCultivo"></param>
        /// <param name="idMunicipio"></param>
        /// <param name="idCultivo"></param>
        /// <param name="fecha"></param>
        /// <returns></returns>
        [HttpGet]
        [Authorize]
        [Route("api/DatosHidricos/{idRegante}/{idUnidadCultivo}/{idMunicipio}/{idCultivo}/{fecha}")]
        public IActionResult GetDatosHidricosList(int? idRegante, string idUnidadCultivo, int? idMunicipio, string idCultivo, string fecha) {
            try {
                int idUsuario;
                string role;
                if (!User.TryLeer(out idUsuario, out role))
                    return Unauthorized();

                return CacheDatosHidricos.Cache(Request.Path.ToString() + "Usuario" + idUsuario, () => {
                    object lDatosHidricos = BalanceHidrico.DatosHidricosList(idRegante, idUnidadCultivo, idMunicipio, idCultivo, fecha, role, idUsuario);
                    return Ok(lDatosHidricos);
                });

            } catch (Exception ex) {
                Log.Error("api/DatosHidricos (lista) - UC:" + idUnidadCultivo + " fecha:" + fecha, ex);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Retornar los Riegos de una unidad de cultivo en una temporada
        /// </summary>
        /// <param name="idUnidadCultivo"></param>
        /// <param name="fecha"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/Riegos/{idUnidadCultivo}/{fecha}")]
        public IActionResult GetRiegos(string idUnidadCultivo, string fecha) {
            try {
                DateTime dFecha = DateTime.Parse(fecha);
                string idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, dFecha);
                if (idTemporada == null)
                    return BadRequest("La unidad de cultivo no está definida para la temporada");

                int idUsuario;
                string role;
                if (!User.TryLeer(out idUsuario, out role))
                    return Unauthorized();
                if (!DB.EstaAutorizado(idUsuario, role, idUnidadCultivo, idTemporada))
                    return Unauthorized();

                return CacheDatosHidricos.Cache(Request.Path.ToString() + "Usuario" + idUsuario.ToString(), () => {
                    return Ok(DB.DatosRiegosList(idUnidadCultivo, idTemporada));
                });

            } catch (Exception ex) {
                Log.Error("api/Riegos - UC:" + idUnidadCultivo + " fecha:" + fecha, ex);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Retornar las lluvias registradas para una unidad de cultivo en una temporada
        /// </summary>
        /// <param name="idUnidadCultivo"></param>
        /// <param name="fecha"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/Lluvias/{idUnidadCultivo}/{fecha}")]
        public IActionResult GetLluvias(string idUnidadCultivo, string fecha) {
            try {
                int idUsuario;
                string role;
                if (!User.TryLeer(out idUsuario, out role))
                    return Unauthorized();

                // La comprobación de acceso se hace fuera de la caché: si estuviera dentro
                // se estaría memorizando la respuesta 401 bajo la clave de la petición.
                string idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, DateTime.Parse(fecha));
                if (!DB.EstaAutorizado(idUsuario, role, idUnidadCultivo, idTemporada))
                    return Unauthorized();

                return CacheDatosHidricos.Cache(Request.Path.ToString() + "Usuario" + idUsuario.ToString(), () => {
                    return Ok(DB.DatosLluviaList(idUnidadCultivo, idTemporada));
                });

            } catch (Exception ex) {
                Log.Error("api/Lluvias - UC:" + idUnidadCultivo + " fecha:" + fecha, ex);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        ///  ResumenDiario
        /// </summary>
        /// <param name="idUnidadCultivo">The idUnidadCultivo<see cref="string"/></param>
        /// <param name="fecha"></param>
        /// <returns>The <see cref="IActionResult"/></returns>
        [HttpGet]
        [Authorize]
        [Route("api/ResumenDiario/{idUnidadCultivo}/{fecha}")]
        public IActionResult ResumenDiario(string idUnidadCultivo, string fecha) {
            try {
                DateTime dFecha = DateTime.Parse(fecha);
                int idUsuario;
                string role;
                if (!User.TryLeer(out idUsuario, out role))
                    return Unauthorized();
                string idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, dFecha);
                if (!DB.EstaAutorizado(idUsuario, role, idUnidadCultivo, idTemporada))
                    return Unauthorized();

                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    BalanceHidrico bh = BalanceHidrico.Balance(idUnidadCultivo, dFecha);
                    var ret = Ok(bh.ResumenDiario(dFecha));
                    return ret;
                });

            } catch (Exception ex) {
                Log.Error("api/ResumenDiario - UC:" + idUnidadCultivo + " fecha:" + fecha, ex);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Fuerza el recálculo completo de la caché de balances. Operación costosa: sólo administradores.
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("api/Recalcula/")]
        public IActionResult Recalcula() {
            try {
                if (!User.EsAdmin())
                    return Unauthorized();
                Log.Info("Recálculo completo de la caché solicitado manualmente");
                CacheDatosHidricos.RecreateAll();
                return Ok("OK");
            } catch (Exception ex) {
                Log.Error("api/Recalcula", ex);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Recalcula la tabla de suelos por unidad de cultivo. Operación costosa y destructiva: sólo administradores.
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("api/RecalculaSuelos/")]
        public IActionResult RecalculaSuelos() {
            try {
                if (!User.EsAdmin())
                    return Unauthorized();
                Log.Info("Recálculo de suelos solicitado manualmente");
                CacheDatosHidricos.RecalculaSuelos();
                return Ok("OK");
            } catch (Exception ex) {
                Log.Error("api/RecalculaSuelos", ex);
                return BadRequest(ex.Message);
            }
        }


        [Authorize]
        [HttpGet]
        [Route("api/UnidadCultivoSuelo/{idUnidadCultivo}/{idTemporada}")]
        public IActionResult UnidadCultivoSuelo(string idUnidadCultivo, string idTemporada) {
            try {
                int idUsuario;
                string role;
                if (!User.TryLeer(out idUsuario, out role))
                    return Unauthorized();
                string uc = idUnidadCultivo.Unquoted();
                string temporada = idTemporada.Unquoted();
                if (!DB.EstaAutorizado(idUsuario, role, uc, temporada))
                    return Unauthorized();

                var lds = DB.UnidadCultivoSueloListNew(uc, temporada);
                if (lds != null && lds.Count > 0) {
                    var lRet = lds.Select(x => new { x.Arcilla, x.Arena, x.Limo, x.MateriaOrganica, x.ElementosGruesos, x.ProfundidadCM});
                    return Ok(lRet);
                } else
                    return Ok("No se encontraron valores de suelo");
            } catch (Exception ex) {
                Log.Error("api/UnidadCultivoSuelo - UC:" + idUnidadCultivo + " temporada:" + idTemporada, ex);
                return BadRequest(ex.Message);
            }
        }
    }
}
