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
        /// Guarda los datos climáticos que llegan del SIAR, ESCRIBIENDO SOLO LO QUE CAMBIA.
        ///
        /// Cada pasada se pide al SIAR una ventana de varios días hacia atrás
        /// (ActualizarDatosClimaticosNDias), porque el SIAR corrige y completa días ya
        /// publicados. Pero la inmensa mayoría de esos días vuelven idénticos: escribirlos
        /// todos con Save reescribía cada mañana ~5 días por estación sin ningún cambio real.
        /// Eso no molesta hoy, pero deja sin base cualquier invalidación por fecha —todo
        /// aparecería tocado a diario— y de paso ensucia la auditoría de la tabla.
        /// </summary>
        /// <param name="lDatClima">Los días que ha devuelto el SIAR.</param>
        /// <returns>
        /// Las fechas en las que de verdad ha cambiado algo (altas y modificaciones).
        /// Vacía si el SIAR ha devuelto lo mismo que ya había.
        /// </returns>
        private static List<DateTime> DatosClimaticosSave(List<DatoClimatico> lDatClima) {
            var cambiadas = new List<DateTime>();
            if (lDatClima == null || lDatClima.Count == 0)
                return cambiadas;

            Database db = DB.ConexionOptiaqua;
            DateTime desde = lDatClima.Min(x => x.Fecha).Date;
            DateTime hasta = lDatClima.Max(x => x.Fecha).Date;
            int idEstacion = lDatClima[0].IdEstacion;

            // Una sola lectura del tramo, en vez de una por día.
            var existentes = db.Fetch<DatoClimatico>(
                    "select Fecha, IdEstacion, TempMedia, HumedadMedia, VelViento, Precipitacion, Eto" +
                    " from DatoClimatico where Fecha between @0 and @1 and IdEstacion=@2", desde, hasta, idEstacion)
                .ToDictionary(x => x.Fecha.Date);

            foreach (DatoClimatico datCli in lDatClima) {
                if (!existentes.TryGetValue(datCli.Fecha.Date, out var actual)) {
                    db.Insert(datCli);
                    cambiadas.Add(datCli.Fecha.Date);
                    continue;
                }
                if (EsElMismoDato(actual, datCli))
                    continue;
                db.Update(datCli);
                cambiadas.Add(datCli.Fecha.Date);
            }
            return cambiadas;
        }

        /// <summary>
        /// Compara dos días de la misma estación campo a campo.
        /// La comparación es por igualdad exacta salvo una tolerancia mínima: el valor viaja
        /// como double y se guarda en una columna float, así que el viaje de ida y vuelta es
        /// exacto; la tolerancia solo cubre el ruido de la última cifra binaria.
        /// </summary>
        private static bool EsElMismoDato(DatoClimatico a, DatoClimatico b) {
            const double tolerancia = 1e-9;
            return Math.Abs(a.TempMedia - b.TempMedia) < tolerancia
                && Math.Abs(a.HumedadMedia - b.HumedadMedia) < tolerancia
                && Math.Abs(a.VelViento - b.VelViento) < tolerancia
                && Math.Abs(a.Precipitacion - b.Precipitacion) < tolerancia
                && Math.Abs(a.Eto - b.Eto) < tolerancia;
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

                var totalCambiadas = new List<DateTime>();
                foreach (var idEstacion in lEstaciones) {
                    List<DatoClimatico> datClima = Siar.DatosClimaticos.DatosClimaticosSiarList_V2(desdeFecha, hastaFecha, idEstacion);
                    var cambiadas = DB.DatosClimaticosSave(datClima);
                    if (cambiadas.Count > 0) {
                        totalCambiadas.AddRange(cambiadas);
                        Log.Info($"SIAR estación {idEstacion}: {cambiadas.Count} día(s) con cambios, desde {cambiadas.Min():dd/MM/yyyy}" +
                                 $" (se han pedido {(hastaFecha - desdeFecha).Days + 1})");
                    }
                    // datClima vacío == el SIAR no ha devuelto nada para esta estación. Antes se
                    // le pedía el Max a una lista vacía, que lanza, y como el try envuelve el
                    // bucle entero se quedaban sin actualizar TODAS las estaciones siguientes.
                    if (datClima.Count == 0 || (nDiasAtras + 1 != datClima.Count && datClima.Max(x => x.Fecha) != DateTime.Today)) {
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
                if (totalCambiadas.Count == 0)
                    Log.Info($"SIAR: {lEstaciones.Count} estaciones consultadas desde {desdeFecha:dd/MM/yyyy}, ningún dato ha cambiado.");
                else
                    Log.Info($"SIAR: {totalCambiadas.Count} día-estación con cambios; el más antiguo, {totalCambiadas.Min():dd/MM/yyyy}.");
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
