namespace WebApi {
    using DatosOptiaqua;
    using Models;
    using System;
    using System.Security.Claims;
    
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Authorization;
    using webapi.Utiles;

    /// <summary>
    /// Sistema de avisos.
    /// </summary>
    public class MultimediaController : ControllerBase {
    
        [Authorize]        
        [Route("api/Multimedia/{IdMultimedia}/{IdMultimediaTipo}/{FInicio}/{FFin}/{Activa}/{Search}")]
        public IActionResult GetMultimedia(int? IdMultimedia, int? IdMultimediaTipo, string FInicio, string FFin, int? Activa, string Search) {
            try {
                DateTime? ini = null;
                if (FInicio != "''") {
                    ini = DateTime.Parse(FInicio.Unquoted());
                }
                DateTime? fin = null;
                if (FFin != "''") {
                    fin = DateTime.Parse(FFin.Unquoted());
                }
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.MultimediaList(IdMultimedia, IdMultimediaTipo, ini, fin, Activa, Search));
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        [Route("api/MultimediaTipo/{IdMultimediaTipo}/{Search}")]
        public IActionResult GetMultimediaTipo(int? IdMultimediaTipo, string Search) {
            try {
                return CacheDatosHidricos.Cache(Request.Path.ToString(), () => {
                    return Ok(DB.MultimediaTipoList(IdMultimediaTipo, Search));
                });
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpPost]
        [Route("api/Multimedia/")]
        public IActionResult PostMultimedia([FromBody] MultimediaPost multimedia) {
            try {
                bool isAdmin = User.EsAdmin();
                if (isAdmin == false)
                    return Unauthorized();
                CacheDatosHidricos.SetDirtyContainsKey("/Multimedia");
                return Ok(DB.MultimediaSave(multimedia));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpPost]
        [Route("api/MultimediaEliminar/")]
        public IActionResult MultimediaEliminar([FromBody] int idMultimedia) {
            try {
                bool isAdmin = User.EsAdmin();
                if (isAdmin == false)
                    return Unauthorized();
                CacheDatosHidricos.SetDirtyContainsKey("/Multimedia");
                return Ok(DB.MultimediaDelete(idMultimedia));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpPost]
        [Route("api/MultimediaTipo/")]
        public IActionResult PostMultimediaTipo([FromBody] Multimedia_Tipo multimediaTipo) {
            try {
                bool isAdmin = User.EsAdmin();
                if (isAdmin == false)
                    return Unauthorized();
                CacheDatosHidricos.SetDirtyContainsKey("/Multimedia");
                return Ok(DB.MultimediaTipoSave(multimediaTipo));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpPost]
        [Route("api/MultimediaTipoEliminar/")]
        public IActionResult MultimediaTipoEliminar([FromBody] int idMultimediaTipo) {
            try {
                bool isAdmin = User.EsAdmin();
                if (isAdmin == false)
                    return Unauthorized();
                CacheDatosHidricos.SetDirtyContainsKey("/Multimedia");
                return Ok(DB.MultimediaTipoDelete(idMultimediaTipo));
            } catch (Exception ex) {
                return BadRequest(ex.Message);
            }
        }

    }
}
