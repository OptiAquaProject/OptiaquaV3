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
    /// Riegos: lectura desde la API de riegos y desde Nebula, hidrantes y las
    /// conversiones entre horas, metros cúbicos y milímetros.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        /// <summary>
        /// The ConversionHorasRiegoAM3.
        /// </summary>
        /// <param name="horasRiego">The horasRiego<see cref="double"/>.</param>
        /// <param name="idUnidadCultivo">The idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="fecha">The fecha<see cref="DateTime"/>.</param>
        /// <returns>The <see cref="double"/>.</returns>
        private static double ConversionHorasRiegoAM3(double horasRiego, string idUnidadCultivo, DateTime fecha) {
            string idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, fecha);
            double superficieM2 = UnidadCultivoExtensionM2(idUnidadCultivo, idTemporada);
            double pluviometriaRiego = DB.UnidadCultivoCultivo(idUnidadCultivo, idTemporada).Pluviometria;
            double m3 = horasRiego * pluviometriaRiego * superficieM2 / 1000;
            return m3;
        }

        /// <summary>
        /// The ConversionM3AHorasRiego.
        /// </summary>
        /// <param name="m3">The m3<see cref="double"/>.</param>
        /// <param name="idUnidadCultivo">The idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="fecha">The fecha<see cref="DateTime"/>.</param>
        /// <returns>The <see cref="double"/>.</returns>
        private static double ConversionM3AHorasRiego(double m3, string idUnidadCultivo, DateTime fecha) {
            string idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, fecha);
            if (idTemporada == null)
                throw new Exception("No hay definida una temporada para unidad de cultivo y fecha.");
            double supertificeM2 = UnidadCultivoExtensionM2(idUnidadCultivo, idTemporada);
            double pluviometriaRiego = DB.UnidadCultivoCultivo(idUnidadCultivo, idTemporada).Pluviometria;
            double divisor = pluviometriaRiego * supertificeM2 / 1000;
            double horasRiego = 0;
            if (divisor != 0)
                horasRiego = m3 / divisor;
            return horasRiego;
        }

        /// <summary>
        /// The ConversionM3RiegoAMm.
        /// </summary>
        /// <param name="m3">The m3<see cref="double"/>.</param>
        /// <param name="idUnidadCultivo">The idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="fecha">The fecha<see cref="DateTime"/>.</param>
        /// <returns>The <see cref="double"/>.</returns>
        private static double ConversionM3RiegoAMm(double m3, string idUnidadCultivo, DateTime fecha) {
            string idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, fecha);
            double superficieM2 = UnidadCultivoExtensionM2(idUnidadCultivo, idTemporada);
            double mm = m3 * 1000 / superficieM2;
            return mm;
        }

        /// <summary>
        /// The ConversionMmRiegoAM3.
        /// </summary>
        /// <param name="mmRiego">The mmRiego<see cref="double"/>.</param>
        /// <param name="idUnidadCultivo">The idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="fecha">The fecha<see cref="DateTime"/>.</param>
        /// <returns>The <see cref="double"/>.</returns>
        private static double ConversionMmRiegoAM3(double mmRiego, string idUnidadCultivo, DateTime fecha) {
            string idTemporada = DB.TemporadaDeFecha(idUnidadCultivo, fecha);
            double superficieM2 = UnidadCultivoExtensionM2(idUnidadCultivo, idTemporada);
            double m3 = mmRiego / 1000 * superficieM2;
            return m3;
        }

        /// <summary>
        /// DatosRiegosList.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="object"/>.</returns>
        public static object DatosRiegosList(string idUnidadCultivo, string idTemporada) {
            List<DatosRiego> redDatosRiegos = new List<DatosRiego>();
            Temporada t = Temporada(idTemporada);
            UnidadCultivo uc = UnidadCultivo(idUnidadCultivo);
            UnidadCultivoCultivo ucc = UnidadCultivoCultivo(idUnidadCultivo, idTemporada);

            DateTime desdeFecha = ucc?.FechaSiembra() ?? t.FechaInicial;
            DateTime hastaFecha = t.FechaFinal < DateTime.Today ? DateTime.Today : t.FechaFinal;


            List<Riego> lRiegos = RiegosList(idUnidadCultivo, desdeFecha, hastaFecha);

            double superficie = DB.UnidadCultivoExtensionM2(idUnidadCultivo, idTemporada) / 1000;
            if (superficie == 0)
                superficie = double.MinValue;

            foreach (Riego r in lRiegos)
                redDatosRiegos.Add(new DatosRiego {
                    Fecha = r.Fecha,
                    M3 = r.RiegoM3,
                    Mm = (r.RiegoM3) / superficie,
                    Obtencion = "S",
                    IdTemporada = t.IdTemporada,
                    IdUnidadCultivo = idUnidadCultivo,
                    UnidadCultivo = uc?.Alias ?? ""
                });

            List<UnidadCultivoDatosExtra> lExtra = DatosExtraList(idUnidadCultivo);
            foreach (UnidadCultivoDatosExtra extra in lExtra)
                if (extra.Fecha >= desdeFecha && extra.Fecha <= hastaFecha && (extra.RiegoM3 ?? 0) > 0) {
                    DatosRiego find = redDatosRiegos.Find(f => f.Fecha == extra.Fecha);
                    if (find != null)
                        redDatosRiegos.Remove(find);
                    redDatosRiegos.Add(new DatosRiego {
                        Fecha = extra.Fecha,
                        M3 = extra.RiegoM3 ?? 0,
                        Mm = (extra.RiegoM3 ?? 0) / superficie,
                        Obtencion = "A",
                        IdTemporada = t.IdTemporada,
                        IdUnidadCultivo = idUnidadCultivo,
                        UnidadCultivo = uc.Alias
                    });
                }
            List<DatosLLuviaORiego> ret = new List<DatosLLuviaORiego>();
            foreach (DatosRiego rie in redDatosRiegos) {
                DatosLLuviaORiego dat = new DatosLLuviaORiego {
                    IdTipoAportacion = "Riego",
                    Fecha = rie.Fecha,
                    IdTemporada = rie.IdTemporada,
                    IdUnidadCultivo = rie.IdUnidadCultivo,
                    Mm = rie.Mm,
                    M3 = rie.M3,
                    Obtencion = rie.Obtencion,
                    UnidadCultivo = rie.IdUnidadCultivo
                };
                ret.Add(dat);
            }
            return ret;
        }

        public static List<Riego> GetRiegos(DateTime desde, DateTime hasta) {
            RefreshDBRiegos();
            Database db = DB.ConexionOptiaqua;
            var lRiegos = db.Fetch<Riego>("SELECT * FROM RIEGO WHERE FECHA>=@0 and FECHA<=@1 ORDER BY FECHA", desde, hasta);
            return lRiegos;
        }

        public static List<Riego> GetRiegosFromApi(DateTime desde, DateTime hasta) {
            try {
                var desdeStr = desde.ToString("yyyy-MM-dd");
                var hastaStr = hasta.ToString("yyyy-MM-dd");
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://optiaqua.dyndns.org/RegantesS3Api/api/riegos/{desdeStr}/{hastaStr}");
                request.Headers.Add("API_KEY", "");
                var response = client.SendAsync(request).Result;
                response.EnsureSuccessStatusCode();
                var json = response.Content.ReadAsStringAsync().Result;
                var riegos = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Riego>>(json);
                return riegos;
            } catch {
                return null;
            }
        }

        public static List<Riego> GetRigosNebula(string IdUnidadCultivo, DateTime desde, DateTime hasta) {
            Database db = DB.ConexionOptiaqua;
            var lRiegos = db.Fetch<Riego>("SELECT * FROM RIEGO WHERE FECHA>=@0 and FECHA<=@1 and IdUnidadCultivo=@2 ORDER BY FECHA", desde, hasta, IdUnidadCultivo);
            return lRiegos;
        }

        public static string HidrantesListJson(string IdUnidadCultivo, string idTemporada) {
            return "[]";
        }

        /// <summary>
        /// Lee la tabla de trabajo tempHidrantes y recorre los hidrantes distintos.
        /// Sin efecto: el cuerpo del bucle está vacío, la importación quedó a medias.
        /// </summary>
        internal static void ImportarHidrantes() {
            Database db = ConexionOptiaqua;
            List<Dictionary<string, string>> ldat = db.Fetch<Dictionary<string, string>>("select * from tempHidrantes");
            List<string> lUcsRepetidas = ldat.Select(x => x["Hid"]).ToList();
            List<string> lUcs = lUcsRepetidas.Distinct().ToList();
            foreach (string idUc in lUcs) {

            }
        }

        public static void RefreshDBRiegos() {
            DateTime? ultimaActualizacion = Config.GetDateTime("FechaUltimaActualizacionApiRiegos");
            if (ultimaActualizacion == null || ultimaActualizacion < DateTime.Today) {
                DateTime desde, hasta;
                if (ultimaActualizacion == null) {
                    desde = new DateTime(1995, 1, 1);
                    hasta = DateTime.Today;
                } else {
                    hasta = ultimaActualizacion.Value;
                    desde = hasta.AddDays(-5);
                }
                List<Riego> lRiegos = GetRiegosFromApi(desde, hasta);
                var minDateRiegos = lRiegos.Min(x => x.Fecha);
                Database db = DB.ConexionOptiaqua;
                int nDel = db.Delete<Riego>("WHERE FECHA>=@0", minDateRiegos); //solo elimino desde la fecha mínima de los riegos que he tradio (hidraplan elimina en cada temporada)
                db.InsertBulk(lRiegos);
                Config.SetDateTime("FechaUltimaActualizacionApiRiegos", DateTime.Today);
            }
        }

        /// <summary>
        /// Retorno lista de riegos.
        /// </summary>
        /// <param name="idUnidadCultivo">.</param>
        /// <param name="fechaSiembra">.</param>
        /// <param name="fechaFinal">.</param>
        /// <returns>.</returns>
        public static List<Riego> RiegosList(string idUnidadCultivo, DateTime fechaSiembra, DateTime fechaFinal) {
            if (idUnidadCultivo == null || fechaSiembra == null || fechaFinal == null)
                return null;
            RefreshDBRiegos();
            Database db = DB.ConexionOptiaqua;

            #region Unidad de cultivo virtual
            var idUnidadCultivoVirtual = db.SingleById<UnidadCultivo>(idUnidadCultivo)?.TomarRiegosDeIdCultivo;
            if (!string.IsNullOrWhiteSpace(idUnidadCultivoVirtual))
                idUnidadCultivo = idUnidadCultivoVirtual;
            #endregion

            string sql = "WHERE idUnidadCultivo=@0 and fecha BETWEEN @1 and @2";
            return db.Fetch<Riego>(sql, idUnidadCultivo, fechaSiembra, fechaFinal);
        }

        /// <summary>
        /// RiegoTipo.
        /// </summary>
        /// <param name="idTipoRiego">idTipoRiego<see cref="int?"/>.</param>
        /// <returns><see cref="RiegoTipo"/>.</returns>
        public static RiegoTipo RiegoTipo(int? idTipoRiego) {
            if (idTipoRiego == null)
                return null;
            Database db = DB.ConexionOptiaqua;
            return db.SingleById<RiegoTipo>(idTipoRiego);
        }
    }
}
