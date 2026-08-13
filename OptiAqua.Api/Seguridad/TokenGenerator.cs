using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Models;

namespace webapi {
    /// <summary>
    /// Parámetros de firma del token, fijados en el arranque a partir de la configuración.
    /// Antes se leían del Web.config con ConfigurationManager en cada emisión.
    /// </summary>
    public static class OpcionesJwt {
        public static string ClaveSecreta { get; private set; }
        public static string Emisor { get; private set; }
        public static string Audiencia { get; private set; }
        public static int CaducidadMinutos { get; private set; }

        public static void Configura(string claveSecreta, string emisor, string audiencia, int caducidadMinutos) {
            if (string.IsNullOrWhiteSpace(claveSecreta))
                throw new ArgumentException("La clave de firma JWT no puede estar vacía", nameof(claveSecreta));
            ClaveSecreta = claveSecreta;
            Emisor = emisor;
            Audiencia = audiencia;
            CaducidadMinutos = caducidadMinutos > 0 ? caducidadMinutos : 43200; // 30 días
        }
    }

    /// <summary>
    /// JWT Token generator class using "secret-key"
    /// more info: https://self-issued.info/docs/draft-ietf-oauth-json-web-token.html
    /// </summary>
    public static class TokenGenerator {
        public static string GenerateTokenJwt(Regante regante) {
            // UTF8 explícito. En .NET Framework se usaba Encoding.Default (la ANSI del sistema);
            // como la clave es ASCII los bytes coinciden y los tokens ya emitidos siguen valiendo.
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(OpcionesJwt.ClaveSecreta));
            var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);

            // create a claimsIdentity
            var claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim("NifRegante", regante.NIF ?? ""));
            claimsIdentity.AddClaim(new Claim("IdRegante", regante.IdRegante.ToString()));
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, regante.Role));

            // create token to the user
            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtSecurityToken = tokenHandler.CreateJwtSecurityToken(
                audience: OpcionesJwt.Audiencia,
                issuer: OpcionesJwt.Emisor,
                subject: claimsIdentity,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(OpcionesJwt.CaducidadMinutos),
                signingCredentials: signingCredentials);

            return tokenHandler.WriteToken(jwtSecurityToken);
        }


        static private readonly List<LoginAcceso> ListaAccesos = new List<LoginAcceso>();

        /// <summary>
        /// La lista es estática y la comparten todas las peticiones: sin sincronizar, dos
        /// intentos de identificación simultáneos pueden corromperla o lanzar excepción.
        /// </summary>
        static private readonly object candadoAccesos = new object();

        /// <summary>
        /// Añadir tiempos de retardo a las peticiones
        /// </summary>
        static public int CalculaRetardo(string nifRegante) {
            lock (candadoAccesos) {
                // eliminar registro de accesos con último acceso con más de 10 minutos.
                var horaCorte = DateTime.Now.AddMinutes(-10);
                ListaAccesos.RemoveAll(x => x.horaUltimoIntento < horaCorte);
                if (ListaAccesos.Count > 1000) { // muchos intentos recientes -> retardo para todas las peticiones
                    return 2000;
                }
                var acceso = ListaAccesos.Find(x => x.nifRegante == nifRegante);
                if (acceso != null) {
                    acceso.nIntentos++;
                    acceso.horaUltimoIntento = DateTime.Now;
                }
                else {
                    acceso = new LoginAcceso { nifRegante = nifRegante, horaUltimoIntento = DateTime.Now, nIntentos = 0 };
                    ListaAccesos.Add(acceso);
                }
                return acceso.nIntentos * acceso.nIntentos * 200; // crece a partir del primer fallo
            }
        }

        /// <summary>
        /// Tope de espera efectiva ante un intento fallido. Cada milisegundo de espera retiene
        /// un hilo, así que un retardo largo es en sí mismo una vía de tumbar el servicio.
        /// </summary>
        public const int RetardoMaximoMs = 2000;
    }
}
