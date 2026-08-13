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
    /// Avisos al regante y ficheros multimedia asociados a las unidades de cultivo.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        /// <summary>
        /// Retorna lista de avisos con los filtros indicados según los parámetros. 
        /// Pasar parámetro con valor '' si no se desea filtrar por campo.
        /// </summary>
        /// <param name="idAviso">idAviso<see cref="string"/>.</param>
        /// <param name="idAvisoTipo">idAvisoTipo<see cref="int?"/>.</param>
        /// <param name="fInicio">fInicio<see cref="DateTime?"/>.</param>
        /// <param name="fFin">fFin<see cref="DateTime?"/>.</param>
        /// <param name="de">de<see cref="string"/>.</param>
        /// <param name="para">para<see cref="string"/>.</param>
        /// <param name="search">search<see cref="string"/>.</param>
        /// <returns><see cref="object"/>.</returns>
        public static object AvisosList(string idAviso, int? idAvisoTipo, DateTime? fInicio, DateTime? fFin, string de, string para, string search) {
            Database db = DB.ConexionOptiaqua;
            idAviso = idAviso.Quoted();
            string strFInicio = fInicio?.ToString().Quoted() ?? "''";
            string strFFin = fFin?.ToString().Quoted() ?? "''";
            string strIdAvisoTipo = idAvisoTipo?.ToString() ?? "''";
            de = de.Quoted();
            para = para.Quoted();
            search = search.Quoted();
            string sql = $"SELECT * FROM AvisosList({idAviso},{strIdAvisoTipo},{strFInicio},{strFFin},{de},{para},{search})";
            return db.Fetch<object>(sql);
        }

        /// <summary>
        /// The MultimediaDelete.
        /// </summary>
        /// <param name="idMultimedia">The idMultimedia<see cref="int"/>.</param>
        /// <returns>The <see cref="object"/>.</returns>
        public static object MultimediaDelete(int idMultimedia) {
            Database db = DB.ConexionOptiaqua;
            db.DeleteWhere<Multimedia>("IdMultimedia=@0", idMultimedia);
            return "OK";
        }

        /// <summary>
        /// The MultimediaList.
        /// </summary>
        /// <param name="idMultimedia">The idMultimedia<see cref="int?"/>.</param>
        /// <param name="idMultimediaTipo">The idMultimediaTipo<see cref="int?"/>.</param>
        /// <param name="fInicio">The fInicio<see cref="DateTime?"/>.</param>
        /// <param name="fFin">The fFin<see cref="DateTime?"/>.</param>
        /// <param name="activa">The activa<see cref="int?"/>.</param>
        /// <param name="search">The search<see cref="string"/>.</param>
        /// <returns>The <see cref="object"/>.</returns>
        public static object MultimediaList(int? idMultimedia, int? idMultimediaTipo, DateTime? fInicio, DateTime? fFin, int? activa, string search) {
            Database db = DB.ConexionOptiaqua;
            string strFInicio = fInicio?.ToString().Quoted() ?? "''";
            string strFFin = fFin?.ToString().Quoted() ?? "''";
            string strIdMultimedia = idMultimedia?.ToString() ?? "''";
            string strIdMultimediaTipo = idMultimediaTipo?.ToString() ?? "''";
            string strActiva = activa?.ToString() ?? "''";
            search = search.Quoted();
            string sql = $"SELECT * FROM MultimediaList({strIdMultimedia},{strIdMultimediaTipo},{strFInicio},{strFFin},{strActiva},{search})";
            return db.Fetch<object>(sql);
        }

        /// <summary>
        /// The MultimediaSave.
        /// </summary>
        /// <param name="multimedia">The multimedia<see cref="MultimediaPost"/>.</param>
        /// <returns>The <see cref="object"/>.</returns>
        public static object MultimediaSave(MultimediaPost multimedia) {
            Database db = DB.ConexionOptiaqua;
            DateTime? fechaExpira = null;
            if (DateTime.TryParse(multimedia.Expira, out DateTime tempFecha))
                fechaExpira = tempFecha;
            Multimedia m = new Multimedia {
                Autor = multimedia.Autor,
                Descripcion = multimedia.Descripcion,
                Expira = fechaExpira,
                Fecha = DateTime.Parse(multimedia.Fecha),
                IdMultimedia = multimedia.IdMultimedia,
                IdMultimediaTipo = multimedia.IdMultimediaTipo,
                Titulo = multimedia.Titulo,
                Url = multimedia.Url
            };
            db.Save(m);
            return m.IdMultimedia.ToString();
        }

        /// <summary>
        /// The MultimediaTipoDelete.
        /// </summary>
        /// <param name="idMultimediaTipo">The idMultimediaTipo<see cref="int"/>.</param>
        /// <returns>The <see cref="object"/>.</returns>
        public static object MultimediaTipoDelete(int idMultimediaTipo) {
            Database db = DB.ConexionOptiaqua;
            db.DeleteWhere<Multimedia_Tipo>("IdMultimediaTipo=@0", idMultimediaTipo);
            return "OK";
        }

        /// <summary>
        /// The MultimediaTipoList.
        /// </summary>
        /// <param name="idMultimediaTipo">The idMultimediaTipo<see cref="int?"/>.</param>
        /// <param name="search">The search<see cref="string"/>.</param>
        /// <returns>The <see cref="object"/>.</returns>
        public static object MultimediaTipoList(int? idMultimediaTipo, string search) {
            Database db = DB.ConexionOptiaqua;
            string strIdMultimediaTipo = idMultimediaTipo?.ToString() ?? "''";
            search = search.Quoted();
            string sql = $"SELECT * FROM MultimediaTipoList({strIdMultimediaTipo},{search})";
            return db.Fetch<object>(sql);
        }

        /// <summary>
        /// The MultimediaTipoSave.
        /// </summary>
        /// <param name="multimediaTipo">The multimediaTipo<see cref="Multimedia_Tipo"/>.</param>
        /// <returns>The <see cref="object"/>.</returns>
        public static object MultimediaTipoSave(Multimedia_Tipo multimediaTipo) {
            Database db = DB.ConexionOptiaqua;
            db.Save(multimediaTipo);
            return multimediaTipo.IdMultimediaTipo.ToString();
        }
    }
}
