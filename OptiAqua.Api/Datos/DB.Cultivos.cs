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
    /// Cultivos, sus etapas fenológicas, fechas de siembra y confirmación, y los
    /// umbrales de estrés hídrico.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        public class cParam {
            public double? TBase { get; set; }
            public double ProfRaizInicial { get; set; }
            public double ProfRaizMax { get; set; }
            public double ModCobCoefA { get; set; }
            public double ModCobCoefB { get; set; }
            public double? ModCobCoefC { get; set; }
            public double ModAltCoefA { get; set; }
            public double ModAltCoefB { get; set; }
            public double? ModAltCoefC { get; set; }
            public double ModRaizCoefA { get; set; }
            public double ModRaizCoefB { get; set; }
            public double? ModRaizCoefC { get; set; }
            public double? AlturaInicial { get; set; }
            public double? AlturaFinal { get; set; }
            public double IntegralEmergencia { get; set; }
            public double CoberturaInicial { get; set; }
            public double CoberturaFinal { get; set; }
        }

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
        /// CultivoEtapasList.
        /// </summary>
        /// <param name="idCultivo">idCultivo<see cref="int?"/>.</param>
        /// <returns><see cref="List{CultivoEtapas}"/>.</returns>
        public static List<CultivoEtapas> CultivoEtapasList(int? idCultivo) {
            if (idCultivo == null)
                return null;
            Database db = DB.ConexionOptiaqua;
            List<CultivoEtapas> listaCF = db.Fetch<CultivoEtapas>("Select * from CultivoEtapas Where IdCultivo=@0", idCultivo);
            return listaCF;
        }

        internal static bool CultivoExists(int idCultivo) {
            using (var db = DB.ConexionOptiaqua) {
                return db.Exists<Cultivo>(idCultivo);
            }
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

        internal static void PropagarEtapas() {
            Database db = DB.ConexionOptiaqua;
            List<UnidadCultivoCultivoEtapas> lCultivoCultivoEtapas = db.Fetch<UnidadCultivoCultivoEtapas>();
            int i = 0;
            foreach (UnidadCultivoCultivoEtapas unidadCultivoCultivoEtapa in lCultivoCultivoEtapas) {
                i++;
                UnidadCultivoCultivo uniCul = new UnidadCultivoCultivo {
                    IdUnidadCultivo = unidadCultivoCultivoEtapa.IdUnidadCultivo,
                    IdTemporada = unidadCultivoCultivoEtapa.IdTemporada
                };
                uniCul = db.SingleById<UnidadCultivoCultivo>(uniCul);

                CultivoEtapas etapa = new CultivoEtapas {
                    IdCultivo = uniCul.IdCultivo,
                    OrdenEtapa = unidadCultivoCultivoEtapa.IdEtapaCultivo
                };
                etapa = db.SingleOrDefaultById<CultivoEtapas>(etapa);
                if (unidadCultivoCultivoEtapa.ParametrosJson != etapa.ParametrosJson) {
                    unidadCultivoCultivoEtapa.ParametrosJson = etapa.ParametrosJson;
                    db.Save(unidadCultivoCultivoEtapa);
                }
            }
        }

        internal static void PropagarEtapas2() {
            Database db = DB.ConexionOptiaqua;
            List<UnidadCultivoCultivoEtapas> lEtapas = db.Fetch<UnidadCultivoCultivoEtapas>();
            int i = 0;
            foreach (UnidadCultivoCultivoEtapas unidadCultivoCultivoEtapa in lEtapas) {
                i++;

                UnidadCultivoCultivo uniCul = new UnidadCultivoCultivo {
                    IdUnidadCultivo = unidadCultivoCultivoEtapa.IdUnidadCultivo,
                    IdTemporada = unidadCultivoCultivoEtapa.IdTemporada
                };
                uniCul = db.SingleById<UnidadCultivoCultivo>(uniCul);

                CultivoEtapas etapa = new CultivoEtapas {
                    IdCultivo = uniCul.IdCultivo,
                    OrdenEtapa = unidadCultivoCultivoEtapa.IdEtapaCultivo
                };
                etapa = db.SingleOrDefaultById<CultivoEtapas>(etapa);
                unidadCultivoCultivoEtapa.AlturaInicial = etapa.AlturaInicial;
                unidadCultivoCultivoEtapa.AlturaFinal = etapa.AlturaFinal;
                unidadCultivoCultivoEtapa.IdTipoCalculoAltura = etapa.IdTipoCalculoAltura;
                unidadCultivoCultivoEtapa.IdTipoCalculoCobertura = etapa.IdTipoCalculoCobertura;
                unidadCultivoCultivoEtapa.IdTipoCalculoLongitudRaiz = etapa.IdTipoCalculoLongitudRaiz;
                //unidadCultivoCultivoEtapa.ParametrosJson = etapa.ParametrosJson;
                db.Save(unidadCultivoCultivoEtapa);

            }
        }

        /*
        internal static void PropagarJsonCultivo() {
            Database db = DB.ConexionOptiaqua;
            List<Cultivo> lCultivos = db.Fetch<Cultivo>();
            foreach (Cultivo c in lCultivos) {
                Dictionary<string, double> dParam = new Dictionary<string, double> {
                    { "ModCobCoefA", c.ModCobCoefA },
                    { "ModCobCoefB", c.ModCobCoefB }
                };
                if (c.ModCobCoefC != null)
                    dParam.Add("ModCobCoefC", c.ModCobCoefC ?? 0);
                dParam.Add("ModAltCoefA", c.ModAltCoefA);
                dParam.Add("ModAltCoefB", c.ModAltCoefB);
                if (c.ModAltCoefC != null)
                    dParam.Add("ModAltCoefC", c.ModAltCoefC ?? 0);
                dParam.Add("ModRaizCoefA", c.ModRaizCoefA);
                dParam.Add("ModRaizCoefB", c.ModRaizCoefB);
                if (c.ModRaizCoefC != null)
                    dParam.Add("ModRaizCoefC", c.ModRaizCoefC ??0);
                
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(dParam, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                c.ParametrosJson = json;
                db.Save(c);
            }
        }
        */

        internal static void QuitarParametrosJson() {
            Database db = DB.ConexionOptiaqua;
            var lEtepas = db.Fetch<UnidadCultivoCultivoEtapas>();
            foreach (var e in lEtepas) {
                Dictionary<string, double> dParam = JsonConvert.DeserializeObject<Dictionary<string, double>>(e.ParametrosJson);
                if (dParam.Keys.Contains("TBase"))
                    dParam.Remove("TBase");

                if (dParam.Keys.Contains("ProfRaizInicial"))
                    dParam.Remove("ProfRaizInicial");

                if (dParam.Keys.Contains("ProfRaizMax"))
                    dParam.Remove("ProfRaizMax");

                if (dParam.Keys.Contains("IntegralEmergencia"))
                    dParam.Remove("IntegralEmergencia");

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(dParam, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                e.ParametrosJson = json;
                db.Save(e);
            }

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
        /// TipoEstresUmbralList.
        /// </summary>
        /// <param name="idTipoEstres">idTipoEstres<see cref="string"/>.</param>
        /// <returns><see cref="List{TipoEstresUmbral}"/>.</returns>
        internal static List<TipoEstresUmbral> TipoEstresUmbralOrderList(string idTipoEstres) {
            Database db = DB.ConexionOptiaqua;
            string sql = $"SELECT * FROM TipoEstresUmbral Where IdTipoEstres='{idTipoEstres}' order by umbralMaximo";
            List<TipoEstresUmbral> ret = db.Fetch<TipoEstresUmbral>(sql);
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
