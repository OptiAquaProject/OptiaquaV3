using NPoco;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using webapi.Utiles;

namespace DatosOptiaqua {

    [TableName("ApiKey")]
    [PrimaryKey("IdApiKey", AutoIncrement = true)]
    public class ApiKeyPoco {
        public int IdApiKey { get; set; }
        /// <summary>Para qué es la clave: qué sistema la usa.</summary>
        public string Descripcion { get; set; }
        /// <summary>
        /// SHA-256 en hexadecimal de la clave. La clave en claro NO se guarda: si se pierde,
        /// se emite otra. Así una copia de la base de datos no entrega las claves de nadie.
        /// </summary>
        public string ClaveHash { get; set; }
        /// <summary>Regante al que representa la clave: de él hereda el role y los permisos.</summary>
        public int IdRegante { get; set; }
        public bool Activa { get; set; }
        public DateTime FechaAlta { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public DateTime? UltimoUso { get; set; }
    }

    /// <summary>Datos de una clave válida, ya resueltos contra el regante que representa.</summary>
    public class ApiKeyValidada {
        public int IdApiKey { get; set; }
        public int IdRegante { get; set; }
        public string Nif { get; set; }
        public string Role { get; set; }
        public string Descripcion { get; set; }
    }

    /// <summary>
    /// Claves de API para sistemas que se integran con OptiAqua sin poder mantener una sesión
    /// con usuario y contraseña (procesos programados, pasarelas de riego, etc.).
    ///
    /// Una clave está siempre asociada a un regante, de modo que la autorización que ya existe
    /// —EstaAutorizado, roles admin/asesor/dbo— funciona igual venga la petición con token JWT
    /// o con clave de API. No es una puerta que se salte los permisos.
    /// </summary>
    public static class ApiKeys {
        private const string PrefijoClave = "oaq_";

        /// <summary>
        /// Genera una clave nueva. Se devuelve en claro UNA sola vez: sólo se guarda su hash.
        /// </summary>
        public static string GeneraClave() {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) {
                rng.GetBytes(bytes);
            }
            // Base64 apto para URL y cabeceras.
            string cuerpo = Convert.ToBase64String(bytes)
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
            return PrefijoClave + cuerpo;
        }

        public static string Hash(string claveEnClaro) {
            if (string.IsNullOrEmpty(claveEnClaro))
                return null;
            using (var sha = SHA256.Create()) {
                byte[] resumen = sha.ComputeHash(Encoding.UTF8.GetBytes(claveEnClaro));
                var sb = new StringBuilder(resumen.Length * 2);
                foreach (byte b in resumen)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Comprueba una clave y devuelve a quién representa, o null si no vale.
        /// Se busca por hash, nunca por la clave en claro.
        /// </summary>
        public static ApiKeyValidada Valida(string claveEnClaro) {
            if (string.IsNullOrWhiteSpace(claveEnClaro))
                return null;
            string hash = Hash(claveEnClaro);
            try {
                using (var db = Conexion.Nueva()) {
                    var registro = db.SingleOrDefault<ApiKeyPoco>(
                        "SELECT * FROM ApiKey WHERE ClaveHash=@0", hash);
                    if (registro == null)
                        return null;
                    if (!registro.Activa) {
                        Log.Aviso("Se ha usado una clave de API revocada (id " + registro.IdApiKey + ")", null);
                        return null;
                    }
                    if (registro.FechaCaducidad != null && registro.FechaCaducidad.Value < DateTime.Now) {
                        Log.Aviso("Se ha usado una clave de API caducada (id " + registro.IdApiKey + ")", null);
                        return null;
                    }

                    var regante = db.SingleOrDefault<Models.Regante>(
                        "SELECT * FROM Regante WHERE IdRegante=@0", registro.IdRegante);
                    if (regante == null) {
                        Log.Aviso("La clave de API " + registro.IdApiKey + " apunta a un regante que ya no existe", null);
                        return null;
                    }

                    // Marca de uso. Que no se pueda anotar no debe invalidar la petición.
                    try {
                        db.Execute("UPDATE ApiKey SET UltimoUso=@0 WHERE IdApiKey=@1", DateTime.Now, registro.IdApiKey);
                    } catch (Exception ex) {
                        Log.Aviso("No se pudo anotar el uso de la clave de API " + registro.IdApiKey, ex);
                    }

                    return new ApiKeyValidada {
                        IdApiKey = registro.IdApiKey,
                        IdRegante = regante.IdRegante,
                        Nif = regante.NIF,
                        Role = regante.Role,
                        Descripcion = registro.Descripcion
                    };
                }
            } catch (Exception ex) {
                Log.Error("Fallo comprobando una clave de API", ex);
                return null;
            }
        }

        /// <summary>
        /// Da de alta una clave y devuelve la clave en claro, que no se podrá volver a consultar.
        /// </summary>
        public static string Crea(string descripcion, int idRegante, DateTime? caducidad) {
            string claveEnClaro = GeneraClave();
            using (var db = Conexion.Nueva()) {
                var registro = new ApiKeyPoco {
                    Descripcion = descripcion,
                    ClaveHash = Hash(claveEnClaro),
                    IdRegante = idRegante,
                    Activa = true,
                    FechaAlta = DateTime.Now,
                    FechaCaducidad = caducidad
                };
                db.Insert(registro);
                Log.Info("Alta de clave de API '" + descripcion + "' para el regante " + idRegante);
            }
            return claveEnClaro;
        }

        /// <summary>Lista las claves SIN el hash: no hay motivo para exponerlo.</summary>
        public static List<ApiKeyPoco> Lista() {
            using (var db = Conexion.Nueva()) {
                var lista = db.Fetch<ApiKeyPoco>(
                    "SELECT IdApiKey, Descripcion, '' AS ClaveHash, IdRegante, Activa, FechaAlta, FechaCaducidad, UltimoUso FROM ApiKey ORDER BY IdApiKey DESC");
                return lista;
            }
        }

        public static bool Revoca(int idApiKey) {
            using (var db = Conexion.Nueva()) {
                int filas = db.Execute("UPDATE ApiKey SET Activa=0 WHERE IdApiKey=@0", idApiKey);
                if (filas > 0)
                    Log.Info("Revocada la clave de API " + idApiKey);
                return filas > 0;
            }
        }

    }
}
