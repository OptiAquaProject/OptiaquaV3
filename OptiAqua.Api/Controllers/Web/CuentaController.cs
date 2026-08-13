namespace WebApi {
    using DatosOptiaqua;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Mvc;
    using Models;
    using System.Collections.Generic;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using webapi.Utiles;

    /// <summary>
    /// Identificación de la web mediante cookie de sesión. Sustituye al parche de pedir usuario y
    /// contraseña en cada acción. Las mismas credenciales del regante (las que usa el móvil) valen
    /// aquí; el rol (admin / asesor / dbo) decide qué ve y qué puede hacer.
    /// </summary>
    public class CuentaController : Controller {
        public const string Esquema = "Cookies";

        [HttpGet]
        public IActionResult Login(string returnUrl) {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string nif, string pass, string returnUrl) {
            Regante regante;
            if (!DB.IsCorrectPassword(new LoginRequest { NifRegante = nif, Password = pass }, out regante) || regante == null) {
                ViewBag.Error = "Usuario o contraseña no válidos";
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.Nif = nif;
                return View();
            }
            var claims = new List<Claim> {
                new Claim("IdRegante", regante.IdRegante.ToString()),
                new Claim("NifRegante", regante.NIF ?? ""),
                new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(regante.Nombre) ? (regante.NIF ?? "") : regante.Nombre),
                new Claim(ClaimTypes.Role, regante.Role ?? "dbo"),
            };
            var identidad = new ClaimsIdentity(claims, Esquema);
            await HttpContext.SignInAsync(Esquema, new ClaimsPrincipal(identidad));
            Log.Info("Login web: " + regante.NIF + " (" + regante.Role + ")");
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Logout() {
            await HttpContext.SignOutAsync(Esquema);
            return RedirectToAction("Index", "Home");
        }
    }
}
