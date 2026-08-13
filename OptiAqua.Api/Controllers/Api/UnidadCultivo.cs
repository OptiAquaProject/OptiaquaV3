namespace WebApi {
    using DatosOptiaqua;
    using Models;
    using System;
    using System.Linq;
    
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Authorization;
    using webapi.Utiles;

    /// <summary>
    /// Proporciona los datos de las unidades de cultivo y las propiedades de su suelo.
    /// </summary>
    public class UnidadCultivoController : ControllerBase {
        /// <summary>
        /// Datos de la unidad de cultivo
        /// </summary>
        /// <param name="idUnidadCultivo"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/UnidadCultivo/{idUnidadCultivo}")]
        public IActionResult Get(string idUnidadCultivo) {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.UnidadCultivo(idUnidadCultivo));
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Lista de las unidades de Cultivo para una temporada
        /// </summary>
        /// <param name="fecha"></param>
        /// <returns></returns>
        [Authorize]
        [Route("api/UnidadesDeCultivo/{fecha}")]
        public IActionResult GetUnidadesDeCultivo(string fecha) {
            try {
                int idUsuario; string rolUsuario;
                if (!User.TryLeer(out idUsuario, out rolUsuario)) return Unauthorized();
                string role = rolUsuario;
                return CacheDatosHidricos.Cache(Request.Path.ToString() + "Usuario" + idUsuario.ToString(), () => {
                    var lTemporadas = DB.TemporadasDeFecha(DateTime.Parse(fecha));
                    return Ok(DB.UnidadesDeCultivoList(lTemporadas, idUsuario, role));
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Lista datos ampliados de unidades de cultivos con filtros
        /// </summary>
        /// <param name="fecha"></param>
        /// <param name="idUnidadCultivo"></param>
        /// <param name="idRegante"></param>
        /// <param name="idCultivo"></param>
        /// <param name="idMunicipio"></param>
        /// <param name="idTipoRiego"></param>
        /// <param name="idEstacion"></param>
        /// <param name="idPoligono"></param>
        /// <param name="idParcela"></param>
        /// <param name="search"></param>     
        /// <returns></returns>

        [Authorize]
        [Route("api/UnidadCultivoList/{Fecha}/{IdUnidadCultivo}/{IdRegante}/{IdCultivo}/{IdMunicipio}/{IdTipoRiego}/{IdEstacion}/{IdPoligono}/{IdParcela}/{Search}")]
        public IActionResult GetUnidadCultivoList(string fecha, string idUnidadCultivo, string idRegante, string idCultivo, string idMunicipio, string idTipoRiego, string idEstacion, string idPoligono, string idParcela, string search) {
            try {
                int idUsuario; string rolUsuario;
                if (!User.TryLeer(out idUsuario, out rolUsuario)) return Unauthorized();
                string role = rolUsuario;


                if (!DateTime.TryParse(fecha.Unquoted(), out var dFecha))
                    dFecha = DateTime.Today;

                return CacheDatosHidricos.Cache(Request.Path.ToString() + "Usuario" + idUsuario.ToString(), () => {
                    var ret = Ok(DB.UnidadCultivoList(dFecha, idUnidadCultivo, idRegante, idCultivo, idMunicipio, idTipoRiego, idPoligono, idParcela, search, idUsuario, role));
                    return ret;
                });

            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Retornar datos ampliados de la unidad de cultivo.
        /// Fecha puede ser '' para presentar todos
        /// IdUnidadCultivo puede ser '' para presentar todos
        /// </summary>
        /// <param name="Fecha"></param>
        /// <param name="IdUnidadCultivo"></param>
        /// <returns></returns>
        [Authorize]
        [HttpGet]
        [Route("api/UnidadCultivoDatosAmpliados/{Fecha}/{IdUnidadCultivo}")]
        public IActionResult GetUnidadCultivoDatosAmpliados(string Fecha, string IdUnidadCultivo) {
            try {
                DateTime FechaEstudio = DateTime.Today;
                if (!string.IsNullOrWhiteSpace(Fecha))
                    FechaEstudio = DateTime.Parse(Fecha);

                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    var ret = Ok(DB.UnidadCultivoDatosAmpliados(FechaEstudio, IdUnidadCultivo.Unquoted()));
                    return ret;
                });


            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        ///  UnidadCultivoTemporadaCosteM3Agua
        /// </summary>
        /// <param name="Fecha">Fecha<see cref="string"/></param>
        /// <param name="IdUnidadCultivo">The IdUnidadCultivo<see cref="string"/></param>
        /// <returns>The <see cref="IActionResult"/></returns>
        [Authorize]
        [HttpGet]
        [Route("api/UnidadCultivoTemporadaCosteM3Agua/{IdUnidadCultivo}/{Fecha}")]
        public IActionResult UnidadCultivoTemporadaCosteM3Agua(string Fecha, string IdUnidadCultivo) {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    var idTemporada = DB.TemporadaDeFecha(IdUnidadCultivo, DateTime.Parse(Fecha));
                    return Ok(DB.UnidadCultivoTemporadaCosteM3Agua(IdUnidadCultivo, idTemporada));
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        ///  PostUnidadCultivoTemporadaCosteM3Agua
        /// </summary>
        /// <param name="param">The param<see cref="ParamPostCosteM3Agua"/></param>
        /// <returns>The <see cref="IActionResult"/></returns>
        [Authorize]
        [HttpPost]
        [Route("api/UnidadCultivoTemporadaCosteM3Agua/")]
        public IActionResult PostUnidadCultivoTemporadaCosteM3Agua([FromBody] ParamPostCosteM3Agua param) {
            try {
                CacheDatosHidricos.SetDirtyUC(param.IdUnidadCultivo);
                return Ok(DB.UnidadCultivoTemporadaCosteM3AguaSave(param));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpGet]
        [Route("api/AsesorUnidadCultivo/{IdRegante}")]
        public IActionResult GetAsesorUnidadCultivo(int idRegante) {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.AsesorUnidadCultivoList(idRegante));
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpPost]
        [Route("api/AsesorUnidadCultivo/")]
        public IActionResult PostAsesorUnidadCultivo([FromBody] ParamAsesorUnidadCultivo param) {
            try {
                param.LUnidadesCultivo = param.LUnidadesCultivo.Replace(";", "#");
                var lUnidadesCultivo = param.LUnidadesCultivo.Split('#').ToList();
                foreach (var iuc in lUnidadesCultivo)
                    CacheDatosHidricos.SetDirtyUC(iuc);
                return Ok(DB.AsesorUnidadCultivoSave(param.IdRegante, lUnidadesCultivo));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpDelete]
        [Route("api/UnidadCultivoDelete/{lIdUnidadCultivos}/{idTemporada}")]
        public IActionResult UnidadCultivoDelete(string lIdUnidadCultivos, string idTemporada) {
            try {
                return Ok(DB.UnidadCultivoDelete(lIdUnidadCultivos, idTemporada));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }
    }
}
