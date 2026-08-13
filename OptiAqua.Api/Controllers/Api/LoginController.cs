using DatosOptiaqua;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using webapi.Utiles;

namespace webapi {

    /// <summary>
    /// login controller class for authenticate users
    /// </summary>
    [ApiController]
    [Route("api/login")]
    public class LoginController : ControllerBase {
        [AllowAnonymous]
        [HttpGet]
        [Route("echoping")]
        public IActionResult EchoPing() {
            return Ok(true);
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("echouser")]
        public IActionResult EchoUser() {
            bool autenticado = User?.Identity != null && User.Identity.IsAuthenticated;
            return Ok($" IPrincipal-user: {User?.Identity?.Name} - IsAuthenticated: {autenticado}");
        }

        /// <summary>
        /// Cambiar password
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("changepassword")]
        public IActionResult ChangePassword(LoginRequest loginChange) {
            try {
                int idReganteEncurso;
                string role;
                if (!User.TryLeer(out idReganteEncurso, out role))
                    return Unauthorized();
                bool isAdmin = role == "admin";
                string nifReganteEnCurso = User.Nif();
                if (isAdmin == false && loginChange.NifRegante != nifReganteEnCurso) {
                    return BadRequest("No es posible cambiar el password de otro usuario sino es administrador");
                }
                if (DB.PasswordSave(loginChange)) {
                    Log.Info("Contraseña cambiada para el usuario " + loginChange.NifRegante);
                    return Ok("Contraseña cambiada satisfactoriamente");
                } else
                    return BadRequest("No se pudo cambiar contraseña");
            }
            catch (Exception ex) {
                Log.Error("api/login/changepassword", ex);
                return BadRequest();
            }
        }

        /// <summary>
        /// Identificar usuario
        /// </summary>
        [AllowAnonymous]
        [HttpPost]
        [Route("authenticate")]
        public IActionResult Authenticate(LoginRequest login) {
            try {
                if (login == null)
                    return BadRequest();

                Regante regante;
                bool isCredentialValid = DB.IsCorrectPassword(login, out regante);
                if (isCredentialValid) {
                    var token = TokenGenerator.GenerateTokenJwt(regante);
                    return Ok(token);
                }
                else {
                    var retardo = TokenGenerator.CalculaRetardo(login.NifRegante);
                    if (retardo > 120 * 1000) // si el tiempo de retardo es muy alto responder error inmediatamente
                        return BadRequest();
                    else {
                        // La espera se limita: retener un hilo decenas de segundos convertía el
                        // mecanismo antiabuso en el propio ataque.
                        System.Threading.Thread.Sleep(Math.Min(retardo, TokenGenerator.RetardoMaximoMs));
                        return Unauthorized();
                    }
                }
            }
            catch (Exception ex) {
                Log.Error("api/login/authenticate", ex);
                return Unauthorized();
            }
        }

        [Authorize(Policy = "Administrador")]
        [HttpGet]
        [Route("LoginAs/{idRegante}")]
        public IActionResult LoginAs(int idRegante) {
            var regante = DB.Regante(idRegante) as Regante;
            if (regante == null)
                return BadRequest("No existe el regante indicado");
            Log.Info("El administrador " + User.Nif() + " ha obtenido un token como el regante " + idRegante);
            var token = TokenGenerator.GenerateTokenJwt(regante);
            return Ok(token);
        }

    }
}
