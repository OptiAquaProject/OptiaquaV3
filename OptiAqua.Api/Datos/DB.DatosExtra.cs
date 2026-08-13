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
    /// Datos extra: campos configurables que se cuelgan de parcelas y unidades
    /// de cultivo sin tocar el esquema.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        /// <summary>
        /// DatosExtraList.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="fecha">fecha<see cref="DateTime"/>.</param>
        /// <returns><see cref="List{UnidadCultivoDatosExtra}"/>.</returns>
        public static UnidadCultivoDatosExtra DatoExtra(string idUnidadCultivo, DateTime fecha) {
            Database db = DB.ConexionOptiaqua;
            string sql;
            sql = "Select * from  UnidadCultivoDatosExtra where IdUnidadCultivo=@0 and fecha=@1";
            var ret = db.Single<UnidadCultivoDatosExtra>(sql, idUnidadCultivo, fecha);
            return ret;
        }

        /// <summary>
        /// DatosExtraList.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <returns><see cref="List{UnidadCultivoDatosExtra}"/>.</returns>
        public static List<UnidadCultivoDatosExtra> DatosExtraList(string idUnidadCultivo) {
            Database db = DB.ConexionOptiaqua;
            string sql;
            sql = "Select * from  UnidadCultivoDatosExtra where IdUnidadCultivo=@0";
            List<UnidadCultivoDatosExtra> ret = db.Fetch<UnidadCultivoDatosExtra>(sql, idUnidadCultivo);
            return ret;
        }

        /// <summary>
        /// The DatosExtraSave.
        /// </summary>
        /// <param name="param">The param<see cref="PostDatosExtraParam"/>.</param>
        public static void DatosExtraSave(PostDatosExtraParam param) {
            try {
                if (DateTime.TryParse(param.Fecha, out DateTime fs) == false) {
                    throw new Exception("Error. El formato de la fecha no es correcto.\n");
                }
                Database db = DB.ConexionOptiaqua;
                UnidadCultivoDatosExtra dat = new UnidadCultivoDatosExtra() { IdUnidadCultivo = param.IdUnidadCultivo, Fecha = fs };
                dat = db.SingleOrDefaultById<UnidadCultivoDatosExtra>(dat);
                if (dat == null)
                    dat = new UnidadCultivoDatosExtra();
                dat.IdUnidadCultivo = param.IdUnidadCultivo;
                dat.Fecha = fs;
                if (param.Cobertura != -1)
                    dat.Cobertura = param.Cobertura;
                if (param.Lluvia != -1)
                    dat.LluviaMm = param.Lluvia;
                if (param.Altura != -1)
                    dat.Altura = param.Altura;
                if (param.DriEnd != -1)
                    dat.DriEnd = param.DriEnd;

                if (param.RiegoM3 != -1) {
                    dat.RiegoM3 = param.RiegoM3;
                    param.RiegoHr = DB.ConversionM3AHorasRiego(param.RiegoM3 ?? 0, param.IdUnidadCultivo, fs);
                    param.RiegoMm = DB.ConversionM3RiegoAMm(param.RiegoM3 ?? 0, param.IdUnidadCultivo, fs);
                } else if (param.RiegoHr != -1) {
                    dat.RiegoM3 = DB.ConversionHorasRiegoAM3((double)param.RiegoHr, param.IdUnidadCultivo, fs);
                    param.RiegoM3 = dat.RiegoM3;
                    param.RiegoMm = param.RiegoM3 / 1000;
                } else if (param.RiegoMm != -1) {
                    dat.RiegoM3 = DB.ConversionMmRiegoAM3((double)param.RiegoMm, param.IdUnidadCultivo, fs);
                    param.RiegoM3 = dat.RiegoM3;
                    param.RiegoHr = DB.ConversionM3AHorasRiego((double)param.RiegoM3, param.IdUnidadCultivo, fs);
                }

                // Si se indica riego 0 m3 se pone a nulo.
                if (dat.RiegoM3 == 0)
                    dat.RiegoM3 = null;

                db.Save(dat);
            } catch (Exception ex) {
                string msgErr = "Error al guardar datos extra.\n ";
                msgErr += ex.Message;
                throw new Exception(msgErr);
            }
        }

        /// <summary>
        /// ParcelasDatosExtrasList.
        /// </summary>
        /// <param name="IdUnidadCultivo">IdUnidadCultivo<see cref="string"/>.</param>
        /// <param name="desdeFecha">desdeFecha<see cref="DateTime"/>.</param>
        /// <param name="hastaFecha">hastaFecha<see cref="DateTime"/>.</param>
        /// <returns><see cref="List{UnidadCultivoDatosExtra}"/>.</returns>
        public static List<UnidadCultivoDatosExtra> ParcelasDatosExtrasList(string IdUnidadCultivo, DateTime desdeFecha, DateTime hastaFecha) {
            if (IdUnidadCultivo == null || desdeFecha == null || hastaFecha == null)
                return null;
            Database db = DB.ConexionOptiaqua;
            string sql = "where fecha BETWEEN @0 AND @1 AND IdUnidadCultivo=@2";
            return db.Fetch<UnidadCultivoDatosExtra>(sql, desdeFecha, hastaFecha, IdUnidadCultivo);
        }
    }
}
