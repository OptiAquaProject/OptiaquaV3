using DatosOptiaqua;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using webapi.Utiles;

namespace WebApi {

    /// <summary>
    /// Alta, consulta y revocación de claves de API. Sólo para administradores.
    /// </summary>
    [ApiController]
    [Route("api/apikeys")]
    [Authorize(Policy = "Administrador")]
    public class ApiKeysController : ControllerBase {

        public class NuevaApiKey {
            /// <summary>Para qué es la clave: qué sistema la va a usar.</summary>
            public string Descripcion { get; set; }
            /// <summary>Regante al que representa: de él hereda role y permisos.</summary>
            public int IdRegante { get; set; }
            /// <summary>Días de validez. 0 o vacío = sin caducidad.</summary>
            public int? DiasValidez { get; set; }
        }

        /// <summary>
        /// Lista las claves emitidas. No se devuelve la clave: sólo se guarda su hash.
        /// </summary>
        [HttpGet]
        public IActionResult Get() {
            return Ok(ApiKeys.Lista());
        }

        /// <summary>
        /// Emite una clave nueva. La clave se devuelve UNA sola vez, en esta respuesta:
        /// no se puede volver a consultar porque en la base de datos sólo queda su hash.
        /// </summary>
        [HttpPost]
        public IActionResult Post([FromBody] NuevaApiKey param) {
            if (param == null || string.IsNullOrWhiteSpace(param.Descripcion))
                return BadRequest("Indique una descripción para la clave");
            if (param.IdRegante <= 0)
                return BadRequest("Indique el regante al que representa la clave");

            var regante = DB.Regante(param.IdRegante) as Models.Regante;
            if (regante == null)
                return BadRequest("No existe el regante indicado");

            DateTime? caducidad = null;
            if (param.DiasValidez != null && param.DiasValidez > 0)
                caducidad = DateTime.Now.AddDays(param.DiasValidez.Value);

            string clave = ApiKeys.Crea(param.Descripcion, param.IdRegante, caducidad);
            Log.Info("El administrador " + User.Nif() + " ha emitido una clave de API para el regante " + param.IdRegante);

            return Ok(new {
                Clave = clave,
                Aviso = "Guarde esta clave ahora: no se puede volver a consultar.",
                Descripcion = param.Descripcion,
                IdRegante = param.IdRegante,
                Role = regante.Role,
                Caducidad = caducidad
            });
        }

        /// <summary>
        /// Revoca una clave. No se borra el registro, para que quede el rastro de que existió.
        /// </summary>
        [HttpDelete]
        [Route("{idApiKey}")]
        public IActionResult Delete(int idApiKey) {
            if (!ApiKeys.Revoca(idApiKey))
                return NotFound("No se encontró la clave indicada");
            Log.Info("El administrador " + User.Nif() + " ha revocado la clave de API " + idApiKey);
            return Ok("Clave revocada");
        }
    }
}
