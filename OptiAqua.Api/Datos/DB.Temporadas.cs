namespace DatosOptiaqua {
    using Models;
    using Newtonsoft.Json;
    using NPoco;
    using Org.BouncyCastle.Crypto;
    using Org.BouncyCastle.Crypto.Signers;
    using System;
    using System.Collections.Generic;
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
    /// Temporadas: listado, temporada activa, existencia y correspondencia entre
    /// fechas y temporadas.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        public class itemTemporadaUC {
            public string IdTemporada { set; get; }
            public string IdUC { set; get; }
        }

        /// <summary>
        /// Temporada.
        /// </summary>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="Temporada"/>.</returns>
        public static Temporada Temporada(string idTemporada) {
            if (string.IsNullOrWhiteSpace(idTemporada))
                return null;
            Database db = DB.ConexionOptiaqua;
            return db.SingleOrDefaultById<Temporada>(idTemporada);
        }

        /// <summary>
        /// TemporadaActiva (si no hay ninguna marcada como activa devuelve la que tiene la fecha final mas alta).
        /// </summary>
        /// <returns><see cref="string"/>.</returns>
        public static string TemporadaActiva() {
            Database db = DB.ConexionOptiaqua;
            string ret = db.Fetch<string>("SELECT IdTemporada from temporada WHERE ACTIVA=1")[0];
            if (ret == null) {
                string sql = " SELECT IdTemporada FROM dbo.Temporada WHERE(FechaFinal = (SELECT TOP(1) MAX(FechaFinal) AS Expr1 FROM dbo.Temporada AS Temporada_1))";
                ret = db.Fetch<string>(sql)[0];
            }
            return ret;
        }

        /// <summary>
        /// TemporadaDeFecha.
        /// </summary>
        /// <param name="idUC">IdUnidadCultivo.</param>
        /// <param name="fecha">fecha<see cref="DateTime"/>.</param>
        /// <returns><see cref="string"/>.</returns>
        public static string TemporadaDeFecha(string idUC, DateTime? fecha) {
            if (fecha == null)
                return DB.TemporadaActiva();
            if (string.IsNullOrWhiteSpace(idUC))
                return DB.TemporadaActiva();
            Database db = DB.ConexionOptiaqua;
            string sql = $"SELECT * FROM TemporadaDeFecha(@0,@1)";
            string ret = db.SingleOrDefault<string>(sql, idUC, fecha);
            if (ret != null) {
                Temporada t = DB.Temporada(ret);
                if (t.FechaInicial > fecha || t.FechaFinal < fecha)
                    ret = null;
            }
            return ret;
        }

        /// <summary>
        /// The TemporadaExists.
        /// </summary>
        /// <param name="idTemporada">The idTemporada<see cref="string"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        internal static bool TemporadaExists(string idTemporada) {
            Database db = DB.ConexionOptiaqua;
            bool ret = db.Exists<Temporada>(idTemporada);
            return ret;
        }

        /// <summary>
        /// Crear o actualizar datos de temporada.
        /// </summary>
        /// <param name="temporada">param<see cref="Temporada"/>.</param>
        /// <returns><see cref="object"/>.</returns>
        public static object TemporadaSave(Temporada temporada) {
            Database db = DB.ConexionOptiaqua;
            db.Save(temporada);
            return "OK";
        }

        /// <summary>
        /// The TemporadasDeFecha.
        /// </summary>
        /// <param name="fecha">The fecha<see cref="DateTime"/>.</param>
        /// <returns>The <see cref="List{string}"/>.</returns>
        public static List<string> TemporadasDeFecha(DateTime fecha) {
            Database db = DB.ConexionOptiaqua;
            string strFecha = fecha.ToString();
            string sql = $"SELECT idTemporada FROM Temporada where @0>=FechaInicial AND @0<=FechaFinal";
            List<string> ret = db.Fetch<string>(sql, fecha);
            return ret;
        }

        /// <summary>
        /// TemporadasList.
        /// </summary>
        /// <returns><see cref="List{Temporada}"/>.</returns>
        public static List<Temporada> TemporadasList() {
            Database db = DB.ConexionOptiaqua;
            string sql = "Select * from Temporada;";
            return db.Fetch<Temporada>(sql);
        }

        /// <summary>
        /// TemporadasUnidadCultivoList.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <returns><see cref="List{string}"/>.</returns>
        public static List<string> TemporadasUnidadCultivoList(string idUnidadCultivo) {
            Database db = DB.ConexionOptiaqua;
            string sql;
            sql = $"Select Distinct IdTemporada from UnidadCultivoCultivo where IdUnidadCultivo ='{idUnidadCultivo}'";
            List<string> ret = db.Fetch<string>(sql);
            return ret;
        }
    }
}
