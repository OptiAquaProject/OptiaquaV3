namespace DatosOptiaqua {
    using Models;
    using NPoco;
    using Org.BouncyCastle.Crypto.Signers;
    using System;
    using System.Configuration;
    using System.Data.SqlTypes;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using webapi;
    using webapi.Utiles;
    using static WebApi.DatosExtraController;

    /// <summary>
    /// Capa de acceso a datos de OptiAqua sobre SQL Server (librería NPoco).
    /// Núcleo: cadena y apertura de conexión, parámetros de configuración
    /// (tabla Config) y registro de eventos.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

#if DEBUG
        public static string CadenaConexionOptiAqua = "CadenaConexionOptiAqua-Debug";


#else
        public static string CadenaConexionOptiAqua = "CadenaConexionOptiAqua";
#endif

        /// <summary>
        /// Gets the ConexionOptiaqua.
        /// </summary>
        public static Database ConexionOptiaqua {
            get {
                
                return Conexion.Nueva();
            }
        }

        /// <summary>
        /// ConfigLoad.
        /// </summary>
        /// <param name="parametro">parametro<see cref="string"/>.</param>
        /// <returns><see cref="string"/>.</returns>
        public static string ConfigLoad(string parametro) {
            Database db = DB.ConexionOptiaqua;
            return db.SingleOrDefaultById<Configuracion>(parametro)?.Valor;
        }

        public static DateTime? ConfigLoadDate(string parametro) {
            Database db = DB.ConexionOptiaqua;
            var valor = db.SingleOrDefaultById<Configuracion>(parametro)?.Valor;
            if (DateTime.TryParse(valor, out var ret))
                return ret;
            return null;
        }

        public static int? ConfigLoadInt(string parametro) {
            Database db = DB.ConexionOptiaqua;
            var strValor = db.SingleOrDefaultById<Configuracion>(parametro)?.Valor;
            if (int.TryParse(strValor, out var ret))
                return ret;
            return null;
        }

        /// <summary>
        /// ConfigSave.
        /// </summary>
        /// <param name="parametro">parametro<see cref="string"/>.</param>
        /// <param name="valor">valor<see cref="string"/>.</param>
        public static void ConfigSave(string parametro, string valor) {
            Database db = DB.ConexionOptiaqua;
            Configuracion cfg = new Configuracion { Parametro = parametro, Valor = valor };
            db.Save(cfg);
        }

        public static int IdEstacionDefault = 505;

        /// <summary>
        /// The InsertaEvento.
        /// </summary>
        /// <param name="txt">The txt<see cref="string"/>.</param>
        internal static void InsertaEvento(string txt) => DB.ConexionOptiaqua.Insert(new EventosPoco { Evento = txt });

        public static string PathRoot { get; set; }
    }
}
