namespace WebApi {
    using DatosOptiaqua;
    using System;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Authorization;
    using webapi.Utiles;

    public class RiegoController : ControllerBase {
        /// <summary>
        /// Riegos registrados para una unidad de cultivo entre dos fechas.
        /// </summary>
        [Authorize]
        [HttpGet]
        [Route("api/riego/{IdUnidadCultivo}/{desdeFecha}/{hastaFecha}")]
        public IActionResult Riego(string IdUnidadCultivo, string desdeFecha, string hastaFecha) {
            try {
                var desde = DateTime.Parse(desdeFecha.Unquoted());
                var hasta = DateTime.Parse(hastaFecha.Unquoted());
                int idUsuario;
                string role;
                if (!User.TryLeer(out idUsuario, out role))
                    return Unauthorized();
                if (!DB.EstaAutorizado(idUsuario, role, IdUnidadCultivo))
                    return Unauthorized();
                return Ok( DB.GetRigosNebula(IdUnidadCultivo,desde, hasta));
            } catch (Exception ex) {
                Log.Error("api/riego - UC:" + IdUnidadCultivo + " " + desdeFecha + " a " + hastaFecha, ex);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Riegos de TODAS las unidades de cultivo entre dos fechas. Sólo administradores.
        /// </summary>
        [Authorize]
        [HttpGet]
        [Route("api/riego/{desdeFecha}/{hastaFecha}")]
        public IActionResult Riego(string desdeFecha, string hastaFecha) {
            try {
                if (!User.EsAdmin())
                    return Unauthorized();
                var desde = DateTime.Parse(desdeFecha.Unquoted());
                var hasta = DateTime.Parse(hastaFecha.Unquoted());
                return Ok(DB.GetRiegos(desde, hasta));
            } catch (Exception ex) {
                Log.Error("api/riego (todas las UC) - " + desdeFecha + " a " + hastaFecha, ex);
                return BadRequest(ex.Message);
            }
        }
    }
}
