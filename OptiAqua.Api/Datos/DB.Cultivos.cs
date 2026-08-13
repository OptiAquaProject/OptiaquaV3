namespace DatosOptiaqua {
    using Models;
    using NPoco;
    using Org.BouncyCastle.Crypto.Signers;
    using System;
    using System.Collections.Generic;
    using System.Data.SqlTypes;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using webapi;
    using webapi.Utiles;
    using static WebApi.DatosExtraController;

    /// <summary>
    /// Capa de acceso a datos de OptiAqua sobre SQL Server (librería NPoco).
    /// Cultivos, sus etapas fenológicas, fechas de siembra y confirmación, y los
    /// umbrales de estrés hídrico.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        /// <summary>
        /// Carga los datos del cultivo referenciado.
        /// </summary>
        /// <param name="idCultivo">.</param>
        /// <returns>.</returns>
        public static Cultivo Cultivo(int? idCultivo) {
            if (idCultivo == null)
                return null;
            Database db = DB.ConexionOptiaqua;
            var ret = db.SingleOrDefaultById<Cultivo>(idCultivo);
            if (ret == null) {
                throw new Exception($"No se encuenta cultivo:{idCultivo}");
            }
            return ret;
        }

        /// <summary>
        /// CultivosList.
        /// </summary>
        /// <returns><see cref="object"/>.</returns>
        public static object CultivosList() {
            Database db = DB.ConexionOptiaqua;
            string sql = "Select * from Cultivo;";
            return db.Fetch<object>(sql);
        }

        /// <summary>
        /// EtapasList.
        /// </summary>
        /// <param name="IdUnidadCultivo">IdUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="List{UnidadCultivoCultivoEtapas}"/>.</returns>
        public static List<UnidadCultivoCultivoEtapas> Etapas(string IdUnidadCultivo, string idTemporada) {
            if (string.IsNullOrWhiteSpace(idTemporada))
                return new List<UnidadCultivoCultivoEtapas>();
            Database db = DB.ConexionOptiaqua;
            string sql = "Select * from UnidadCultivoCultivoEtapas where IdUnidadCultivo=@0  AND IdTemporada=@1";
            List<UnidadCultivoCultivoEtapas> ret = db.Fetch<UnidadCultivoCultivoEtapas>(sql, IdUnidadCultivo, idTemporada);
            return ret;
        }

        /// <summary>
        /// FechaConfirmadaSave.
        /// </summary>
        /// <param name="IdUnidadCultivo">IdUnidadCultivo<see cref="string"/>.</param>
        /// <param name="temporada">temporada<see cref="string"/>.</param>
        /// <param name="nEtapa">nEtapa<see cref="int"/>.</param>
        /// <param name="fechaConfirmada">fechaConfirmada<see cref="DateTime"/>.</param>
        public static void FechaConfirmadaSave(string IdUnidadCultivo, string temporada, int nEtapa, DateTime fechaConfirmada) {
            try {
                Database db = DB.ConexionOptiaqua;
                UnidadCultivoCultivoEtapas dat = new UnidadCultivoCultivoEtapas {
                    IdUnidadCultivo = IdUnidadCultivo,
                    IdTemporada = temporada,
                    IdEtapaCultivo = nEtapa
                };
                dat = db.SingleOrDefaultById<UnidadCultivoCultivoEtapas>(dat);
                if (dat != null) {
                    dat.FechaInicioEtapaConfirmada = fechaConfirmada;
                    db.Save(dat);
                } else {
                    throw new Exception("Error accediendo a UnidadCultivoCultivoEtapas\n.");
                }
            } catch (Exception ex) {
                string msgErr = "Error cargando ParcelasCultivosEtapas.\n ";
                msgErr += ex.Message;
                throw new Exception(msgErr);
            }
        }

        /// <summary>
        /// FechasEtapasSave.
        /// </summary>
        /// <param name="lEtapas">lEtapas<see cref="List{UnidadCultivoCultivoEtapas}"/>.</param>
        public static void FechasEtapasSave(List<UnidadCultivoCultivoEtapas> lEtapas) {
            Database db = null;
            if (lEtapas == null || lEtapas.Count == 0) return;
            try {
                db = DB.ConexionOptiaqua;
                db.BeginTransaction();
                //Eliminar las actuales
                db.Execute(" delete from UnidadCultivoCultivoEtapas where IdUnidadCultivo=@0 and IdTemporada=@1 ", lEtapas[0].IdUnidadCultivo, lEtapas[0].IdTemporada);
                db.InsertBulk<UnidadCultivoCultivoEtapas>(lEtapas);
                db.CompleteTransaction();
            } catch (Exception) {
                if (db != null)
                    db.AbortTransaction();
            }
        }

        /// <summary>
        /// The FechaSiembra.
        /// </summary>
        /// <param name="idUnidadCultivo">The idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">The idTemporada<see cref="string"/>.</param>
        /// <returns>The <see cref="DateTime?"/>.</returns>
        internal static DateTime? FechaSiembra(string idUnidadCultivo, string idTemporada) {
            Database db = DB.ConexionOptiaqua;
            UnidadCultivoCultivoEtapas reg = new UnidadCultivoCultivoEtapas { IdUnidadCultivo = idUnidadCultivo, IdTemporada = idTemporada, IdEtapaCultivo = 1 };
            UnidadCultivoCultivoEtapas ret = db.SingleOrDefaultById<UnidadCultivoCultivoEtapas>(reg);
            return ret?.FechaInicioEtapa;
        }

        /// <summary>
        /// The ListaEstresUmbral.
        /// </summary>
        /// <returns>The <see cref="Dictionary{string, List{TipoEstresUmbral}}"/>.</returns>
        internal static Dictionary<string, List<TipoEstresUmbral>> ListaEstresUmbral() {
            Database db = DB.ConexionOptiaqua;
            List<TipoEstresUmbral> lUmbrales = db.Fetch<TipoEstresUmbral>();
            Dictionary<string, List<TipoEstresUmbral>> ret = new Dictionary<string, List<TipoEstresUmbral>>();
            foreach (TipoEstresUmbral umbral in lUmbrales) {
                if (!ret.Keys.Contains(umbral.IdTipoEstres))
                    ret.Add(umbral.IdTipoEstres, new List<TipoEstresUmbral>());
                ret[umbral.IdTipoEstres].Add(umbral);
            }
            // La clasificación del estrés (TipoEstresUmbral) recorre esta lista y para en el
            // primer umbral que supera el índice, asumiéndola ordenada ascendentemente por
            // UmbralMaximo. El Fetch no lleva ORDER BY, así que se ordena aquí para garantizarlo.
            foreach (var lista in ret.Values)
                lista.Sort((a, b) => a.UmbralMaximo.CompareTo(b.UmbralMaximo));
            return ret;
        }

        /// <summary>
        /// The ListaTipoEstres.
        /// </summary>
        /// <returns>The <see cref="Dictionary{string, TipoEstres}"/>.</returns>
        internal static Dictionary<string, TipoEstres> ListaTipoEstres() {
            Database db = DB.ConexionOptiaqua;
            List<TipoEstres> ret = db.Fetch<TipoEstres>();
            return ret.ToDictionary(x => x.IdTipoEstres);
        }

        /// <summary>
        /// Devuele el registro TipoEstres inficado por su identificador.
        /// </summary>
        /// <param name="idTipoEstres">The idTipoEstres<see cref="string"/>.</param>
        /// <returns>The <see cref="TipoEstres"/>.</returns>
        internal static TipoEstres TipoEstres(string idTipoEstres) {
            Database db = DB.ConexionOptiaqua;
            TipoEstres ret = db.SingleById<TipoEstres>(idTipoEstres);
            return ret;
        }

        /// <summary>
        /// UnidadCultivoCultivoEtapasList.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="List{UnidadCultivoCultivoEtapas}"/>.</returns>
        public static List<UnidadCultivoCultivoEtapas> UnidadCultivoCultivoEtapasList(string idUnidadCultivo, string idTemporada) {
            if (idUnidadCultivo == null || idTemporada == null)
                return null;
            Database db = DB.ConexionOptiaqua;
            string sql;
            sql = "Select * from UnidadCultivoCultivoEtapas where IdUnidadCultivo =@0 AND IDTemporada=@1";
            return db.Fetch<UnidadCultivoCultivoEtapas>(sql, idUnidadCultivo, idTemporada);
        }
    }
}
