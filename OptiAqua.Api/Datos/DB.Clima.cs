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
    /// Datos climáticos: descarga y refresco desde el SIAR de La Rioja, estaciones
    /// asignadas a parcelas y unidades de cultivo, lluvia y pluviometría.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        /// <summary>
        /// DatosClimaticosList.
        /// </summary>
        /// <param name="desdeFecha">desdeFecha<see cref="DateTime?"/>.</param>
        /// <param name="hastaFecha">hastaFecha<see cref="DateTime?"/>.</param>
        /// <param name="idEstacion">idEstacion<see cref="int?"/>.</param>
        /// <returns><see cref="List{DatoClimatico}"/>.</returns>
        public static List<DatoClimatico> DatosClimaticosList(DateTime? desdeFecha, DateTime? hastaFecha, int? idEstacion) {
            if (desdeFecha == null || hastaFecha == null || idEstacion == null)
                return null;
            // Refrescar la base de datos con los datos de Siar si es necesario
            DB.DatosClimaticosSiarRefresh();
            Database db = DB.ConexionOptiaqua;
            string sql = "Select * from DatoClimatico where fecha BETWEEN  @0 AND @1 AND IDESTACION=@2";
            return db.Fetch<DatoClimatico>(sql, desdeFecha, hastaFecha, idEstacion);
        }

        /// <summary>
        /// DatosClimaticosSave.
        /// </summary>
        /// <param name="lDatClima">lDatClima<see cref="List{DatoClimatico}"/>.</param>
        private static void DatosClimaticosSave(List<DatoClimatico> lDatClima) {
            Database db = DB.ConexionOptiaqua;
            foreach (DatoClimatico datCli in lDatClima) {
                db.Save(datCli);
            }
        }

        /// <summary>
        /// Actualiza los datos climáticos almacenados.
        /// Se conecta a el api del SIAR si hace al menos una hora que no lo ha hecho y actualiza los datos desde última acualización.
        /// Si se actualizó hace menos de 4 días actuliza los últimos 4 días.
        /// </summary>
        public static void DatosClimaticosSiarForceRefresh() {
            var emailNotificacionErrorDatosClimaticos = "siar.cida@larioja.org";
            try {
                DateTime? ultimaFechaEnTabla = DB.UltimaFechaDeEstacion();
                List<int> lEstaciones = DB.EstacionesIdList();
                if (ultimaFechaEnTabla == null)
                    ultimaFechaEnTabla = new DateTime(2000, 01, 01);

                var nDiasAtras = DB.ConfigLoadInt("ActualizarDatosClimaticosNDias") ?? 4;
                DateTime desdeFecha = ((DateTime)ultimaFechaEnTabla).AddDays(-nDiasAtras); // Añado 4 días a la lista
                DateTime hastaFecha = DateTime.Today;

                foreach (var idEstacion in lEstaciones) {
                    //hastaFecha = new DateTime(2024, 2, 28);
                    List<DatoClimatico> datClima = Siar.DatosClimaticos.DatosClimaticosSiarList_V2(desdeFecha, hastaFecha, idEstacion);
                    DB.DatosClimaticosSave(datClima);
                    if (nDiasAtras + 1 != datClima.Count && datClima.Max(x => x.Fecha) != DateTime.Today) {
                        emailNotificacionErrorDatosClimaticos = DB.ConfigLoad("EmailNotificacionErrorDatosClimaticos");
                        var subject = "Error en lectura de datos climáticos. Fecha: " + hastaFecha.Date.ToShortDateString();
                        var body = DateTime.Now + " -" + subject;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        var ultimaFechaEnvioEmailError = DB.ConfigLoadDate("ultimaFechaEnvioEmailError");
                        if (ultimaFechaEnvioEmailError == null || DateTime.Today > ultimaFechaEnvioEmailError) {
                            Email.SendMail(emailNotificacionErrorDatosClimaticos, subject, body, null);
                            DB.ConfigSave("ultimaFechaEnvioEmailError", DateTime.Today.ToShortDateString());
                        }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                }
            } catch {
                emailNotificacionErrorDatosClimaticos = DB.ConfigLoad("EmailNotificacionErrorDatosClimaticos");
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Email.SendMail(emailNotificacionErrorDatosClimaticos, "Error en lectura de datos climáticos", "Error indeterminado");
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                // continua sin datos del SIAR
            }
        }

        /// <summary>
        /// Llama a refrescar datos climáticos si aún no se ha hecho a fecha actual.
        /// </summary>
        public static void DatosClimaticosSiarRefresh() {
            DateTime? ultimaFechaActualizacionDatosCliematicosSiar = Config.GetDateTime("FechaUltimaActualizacionSiar");
            if (ultimaFechaActualizacionDatosCliematicosSiar == null || ultimaFechaActualizacionDatosCliematicosSiar.Value.Date < DateTime.Today) {
                DB.DatosClimaticosSiarForceRefresh();
                Config.SetDateTime("FechaUltimaActualizacionSiar", DateTime.Today);
            }
        }

        /// <summary>
        /// DatosLluviaList.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="object"/>.</returns>
        public static object DatosLluviaList(string idUnidadCultivo, string idTemporada) {
            List<DatosLLuvia> retDatosLluvia = new List<DatosLLuvia>();
            Temporada temporada = Temporada(idTemporada);
            if (temporada == null) {
                temporada = Temporada(DB.TemporadaActiva());
            }
            idTemporada = temporada.IdTemporada;

            var idEstacion = DB.EstacionDeUC(idUnidadCultivo, idTemporada);
            UnidadCultivo uc = UnidadCultivo(idUnidadCultivo);
            UnidadCultivoCultivo ucc = UnidadCultivoCultivo(idUnidadCultivo, idTemporada);
            if (ucc == null)
                throw new Exception($"No se encontró la unidad de cultivo '{idUnidadCultivo}' para la temporada'{idTemporada}'");

            DateTime desdeFecha = ucc.FechaSiembra() ?? temporada.FechaInicial;
            DateTime hastaFecha = temporada.FechaFinal < DateTime.Today ? DateTime.Today : temporada.FechaFinal;

            List<DatoClimatico> lLluvia = DatosClimaticosList(desdeFecha, hastaFecha, idEstacion);

            foreach (DatoClimatico ll in lLluvia)
                retDatosLluvia.Add(new DatosLLuvia {
                    Fecha = ll.Fecha,
                    Mm = ll.Precipitacion,
                    Obtencion = "S",
                    IdEstacion = ll.IdEstacion,
                    IdTemporada = temporada.IdTemporada,
                    IdUnidadCultivo = uc.IdUnidadCultivo,
                    UnidadCultivo = uc.Alias,
                });

            List<UnidadCultivoDatosExtra> lExtra = DatosExtraList(idUnidadCultivo);
            foreach (UnidadCultivoDatosExtra extra in lExtra)
                if (extra.Fecha >= desdeFecha && extra.Fecha <= hastaFecha && (extra.LluviaMm ?? 0) > 0) {
                    DatosLLuvia find = retDatosLluvia.Find(f => f.Fecha == extra.Fecha);
                    if (find != null)
                        retDatosLluvia.Remove(find);
                    retDatosLluvia.Add(new DatosLLuvia {
                        Fecha = extra.Fecha,
                        Mm = extra.LluviaMm ?? 0,
                        Obtencion = "A",
                        IdEstacion = idEstacion,
                        IdTemporada = temporada.IdTemporada,
                        IdUnidadCultivo = uc.IdUnidadCultivo,
                        UnidadCultivo = uc.Alias
                    });
                }
            List<DatosLLuviaORiego> ret = new List<DatosLLuviaORiego>();
            foreach (DatosLLuvia llu in retDatosLluvia) {
                DatosLLuviaORiego dat = new DatosLLuviaORiego {
                    IdTipoAportacion = "Lluvia",
                    Fecha = llu.Fecha,
                    IdEstacion = llu.IdEstacion,
                    IdTemporada = llu.IdTemporada,
                    IdUnidadCultivo = llu.IdUnidadCultivo,
                    Mm = llu.Mm,
                    Obtencion = llu.Obtencion,
                    UnidadCultivo = llu.IdUnidadCultivo
                };
                ret.Add(dat);
            }
            return ret;
        }

        /*
        public static void ActulizaEstacionParcelas() {
            using (var db = DB.ConexionOptiaqua) {
                var lPar = db.Fetch<ParcelaPoco>();
                foreach (var par in lPar) {
                    int idEstacion = IdEstacionDefault;
                    double lng = 0, lat = 0;
                    if (par.Latitud == null || par.Longitud == null) {
                        MapasCatastralesDatosLngLat(par, out lat, out lng);
                    } else {
                        var lngStr = par.Longitud.ToString().Replace(',', '.');
                        var latStr = par.Latitud.ToString().Replace(',', '.');
                        var sql = $" select codigoEMA from ZonasInfluenciaEMAsRegGeneral where geom.STContains( geometry::STGeomFromText('POINT({lngStr}  {latStr})', 4326)) =1";
                        idEstacion = db.SingleOrDefault<int>(sql);
                        if (idEstacion == 0)
                            idEstacion = IdEstacionDefault;
                    }
                    var nExe = db.Execute($"update Parcela Set IdEstacion={idEstacion} where IdParcelaInt={par.IdParcelaInt}");
                }

            }
        }
        */

        internal static int EstacionDeParcela(int idParcelaInt) {
            using (var db = DB.ConexionOptiaqua) {
                var par = db.SingleOrDefaultById<ParcelaPoco>(idParcelaInt);
                if (par != null) {
                    return par.IdEstacion ?? IdEstacionDefault;
                }
                return IdEstacionDefault;
            }
        }

        internal static int EstacionDeUC(string idUC, string idTemporada) {
            using (var db = DB.ConexionOptiaqua) {
                string sql = $@"
                    SELECT TOP (1) dbo.Parcela.IdEstacion
                    FROM dbo.UnidadCultivoParcela INNER JOIN
                    dbo.Parcela ON dbo.UnidadCultivoParcela.IdParcelaInt = dbo.Parcela.IdParcelaInt
                    WHERE
                        (dbo.UnidadCultivoParcela.IdUnidadCultivo = N'{idUC}')
                        AND 
                        (dbo.UnidadCultivoParcela.IdTemporada = N'{idTemporada}')
                    ORDER BY dbo.Parcela.SuperficieM2 DESC ";
                var ret = db.SingleOrDefault<int?>(sql);
                if (ret == null || ret == 0)
                    return IdEstacionDefault;
                return ret.Value;
            }
        }

        private static List<int> EstacionesIdList() {
            Database db = DB.ConexionOptiaqua;
            return db.Fetch<int>("select * from EstacionIdList");
        }

        internal static string EstacionNombre(int idEstacion) {
            if (idEstacion <= 0)
                idEstacion = IdEstacionDefault;
            using (var db = DB.ConexionOptiaqua) {
                var nombre = db.Single<string>($"select Nombre from Estacion where IdEstacion={idEstacion}");
                return nombre;
            }

        }

        /// <summary>
        /// PluviometriaSave.
        /// </summary>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="pluviometria">pluviometria<see cref="double"/>.</param>
        public static void PluviometriaSave(string idTemporada, string idUnidadCultivo, double pluviometria) {
            Database db = DB.ConexionOptiaqua;
            UnidadCultivoCultivo unidadCultivoCultivo = db.Single<UnidadCultivoCultivo>("SELECT * FROM UnidadCultivoCultivo WHERE IdTemporada=@0 and idUnidadCultivo=@1");
            unidadCultivoCultivo.Pluviometria = pluviometria;
            db.Save(unidadCultivoCultivo);
        }

        /// <summary>
        /// PluviometriaTipica.
        /// </summary>
        /// <param name="idCultivo">idCultivo<see cref="int"/>.</param>
        /// <returns><see cref="double"/>.</returns>
        public static double PluviometriaTipica(int idCultivo) {
            Database db = DB.ConexionOptiaqua;
            string sql = "Select PluviometriaTipica from RiegoTipo where IdTipoRiego=@0";
            return db.Single<double>(sql, idCultivo);
        }

        /// <summary>
        /// UltimaFechaDeEstacion.
        /// </summary>
        /// <returns><see cref="DateTime?"/>.</returns>
        private static DateTime? UltimaFechaDeEstacion() {
            Database db = DB.ConexionOptiaqua;
            string sql;
            sql = "SELECT MIN(MaxFecha) AS MinFecha FROM dbo.DatoClimaticoMaxFecha";
            return db.Single<DateTime?>(sql);
        }
    }
}
