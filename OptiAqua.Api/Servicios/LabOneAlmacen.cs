namespace DatosOptiaqua {
    using Newtonsoft.Json;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Dónde vive un ensayo de LAB-ONE: en memoria mientras se trabaja con él, y en un JSON en
    /// disco cuando interesa conservarlo.
    ///
    /// En memoria hay UN ensayo por usuario. Es lo que hace falta para un laboratorio —se toca
    /// un dato, se recalcula, se compara— y evita que dos personas se pisen los cambios. Como
    /// vive en el proceso, un reinicio del servidor se lo lleva por delante: por eso está el
    /// botón de guardar en disco, y por eso conviene decirlo en la pantalla en lugar de dejar
    /// que se descubra perdiendo el trabajo.
    ///
    /// Nada de esto toca la base de datos: un ensayo no es un dato de producción.
    /// </summary>
    public static class LabOneAlmacen {

        private static readonly ConcurrentDictionary<string, LabOneEnsayo> enMemoria =
            new ConcurrentDictionary<string, LabOneEnsayo>();

        /// <summary>
        /// Carpeta donde se guardan los JSON. La fija el controlador al arrancar, a partir de la
        /// raíz de contenido de la aplicación.
        /// </summary>
        public static string Carpeta { get; set; }

        /// <summary>El ensayo que ese usuario tiene abierto, o null.</summary>
        /// <param name="usuario">Nombre del usuario identificado.</param>
        public static LabOneEnsayo Abierto(string usuario) {
            enMemoria.TryGetValue(usuario ?? "", out var ret);
            return ret;
        }

        /// <summary>Deja un ensayo como el abierto por ese usuario.</summary>
        /// <param name="usuario">Nombre del usuario identificado.</param>
        /// <param name="ensayo">Ensayo a mantener en memoria.</param>
        public static void Abre(string usuario, LabOneEnsayo ensayo) {
            if (ensayo == null) return;
            ensayo.Modificado = DateTime.Now;
            enMemoria[usuario ?? ""] = ensayo;
        }

        /// <summary>Cierra el ensayo que ese usuario tuviera abierto.</summary>
        /// <param name="usuario">Nombre del usuario identificado.</param>
        public static void Cierra(string usuario) => enMemoria.TryRemove(usuario ?? "", out _);

        /// <summary>
        /// Convierte un nombre cualquiera en un nombre de fichero seguro.
        ///
        /// El nombre lo escribe el usuario y termina formando una ruta: sin esto, un "..\\" o una
        /// ruta absoluta escribirían donde no deben. Se queda solo con letras, dígitos, guiones y
        /// espacios, y la extensión la pone esta función, no quien llama.
        /// </summary>
        /// <param name="nombre">Nombre tal y como lo ha escrito el usuario.</param>
        /// <returns>Nombre de fichero con extensión .json.</returns>
        public static string NombreFichero(string nombre) {
            var sb = new StringBuilder();
            foreach (char c in (nombre ?? "").Trim())
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ' || c == '.')
                    sb.Append(c == '.' ? '_' : c);
            string ret = sb.ToString().Trim();
            if (ret.Length == 0) ret = "ensayo";
            if (ret.Length > 80) ret = ret.Substring(0, 80);
            return ret + ".json";
        }

        /// <summary>Ruta completa de un ensayo guardado, ya saneada.</summary>
        /// <param name="nombre">Nombre del ensayo o del fichero.</param>
        private static string Ruta(string nombre) {
            if (string.IsNullOrEmpty(Carpeta))
                throw new Exception("No se ha configurado la carpeta de ensayos de LAB-ONE.");
            Directory.CreateDirectory(Carpeta);
            return Path.Combine(Carpeta, NombreFichero(
                nombre != null && nombre.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    ? nombre.Substring(0, nombre.Length - 5)
                    : nombre));
        }

        /// <summary>Guarda el ensayo en disco con su propio nombre.</summary>
        /// <param name="ensayo">Ensayo a guardar.</param>
        /// <returns>El nombre del fichero escrito.</returns>
        public static string GuardaEnDisco(LabOneEnsayo ensayo) {
            ensayo.Modificado = DateTime.Now;
            string ruta = Ruta(ensayo.Nombre);
            File.WriteAllText(ruta, ASerializado(ensayo), new UTF8Encoding(false));
            return Path.GetFileName(ruta);
        }

        /// <summary>Lee un ensayo guardado.</summary>
        /// <param name="fichero">Nombre del fichero dentro de la carpeta de ensayos.</param>
        public static LabOneEnsayo LeeDeDisco(string fichero) {
            string ruta = Ruta(fichero);
            if (!File.Exists(ruta))
                throw new Exception("No se encuentra el ensayo " + Path.GetFileName(ruta));
            return DeSerializado(File.ReadAllText(ruta));
        }

        /// <summary>Borra un ensayo guardado.</summary>
        /// <param name="fichero">Nombre del fichero dentro de la carpeta de ensayos.</param>
        public static void BorraDeDisco(string fichero) {
            string ruta = Ruta(fichero);
            if (File.Exists(ruta)) File.Delete(ruta);
        }

        /// <summary>Los ensayos guardados, del más reciente al más antiguo.</summary>
        public static List<LabOneGuardado> Guardados() {
            var ret = new List<LabOneGuardado>();
            if (string.IsNullOrEmpty(Carpeta) || !Directory.Exists(Carpeta)) return ret;
            foreach (var f in Directory.GetFiles(Carpeta, "*.json")) {
                var inf = new FileInfo(f);
                ret.Add(new LabOneGuardado {
                    Fichero = inf.Name,
                    Nombre = Path.GetFileNameWithoutExtension(inf.Name),
                    Modificado = inf.LastWriteTime,
                    Bytes = inf.Length,
                });
            }
            ret.Sort((a, b) => b.Modificado.CompareTo(a.Modificado));
            return ret;
        }

        /// <summary>El ensayo en JSON, con sangrado, que es como se guarda y como se descarga.</summary>
        /// <param name="ensayo">Ensayo a serializar.</param>
        public static string ASerializado(LabOneEnsayo ensayo) =>
            JsonConvert.SerializeObject(ensayo, Formatting.Indented);

        /// <summary>Reconstruye un ensayo desde su JSON.</summary>
        /// <param name="json">Texto JSON.</param>
        public static LabOneEnsayo DeSerializado(string json) {
            var ret = JsonConvert.DeserializeObject<LabOneEnsayo>(json);
            if (ret == null) throw new Exception("El JSON no contiene un ensayo.");
            return ret;
        }
    }

    /// <summary>Una entrada de la lista de ensayos guardados en disco.</summary>
    public class LabOneGuardado {
        public string Fichero { get; set; }
        public string Nombre { get; set; }
        public DateTime Modificado { get; set; }
        public long Bytes { get; set; }
    }
}
