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

    /// <summary>
    /// Capa de acceso a datos de OptiAqua sobre SQL Server (librería NPoco).
    /// Parcelas: alta y consulta, referencia catastral y SigPac, geometría y
    /// superficie, y los catálogos de provincia, municipio y paraje.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        private static void DatosParcelasList(string idUnidadCultivo, string idTemporada, out string poligonos, out string parcelas, out string refCatastrales) {
            Database db = DB.ConexionOptiaqua;
            string sql = " SELECT Parcela.RefCatastral, Parcela.IdPoligono, Parcela.IdParcela FROM Parcela";
            sql += " INNER JOIN UnidadCultivoParcela ON Parcela.IdParcelaInt = UnidadCultivoParcela.IdParcelaInt ";
            sql += " WHERE(UnidadCultivoParcela.IdUnidadCultivo = @0) AND (UnidadCultivoParcela.IdTemporada = @1)";
            List<RefCatasPolPar> lRefCatasPolPar = db.Fetch<RefCatasPolPar>(sql, idUnidadCultivo, idTemporada);
            poligonos = string.Join("#", lRefCatasPolPar.Select(x => x.IdPoligono.Trim()).Distinct());
            parcelas = string.Join("#", lRefCatasPolPar.Select(x => x.IdParcela.Trim()).Distinct());
            refCatastrales = string.Join("#", lRefCatasPolPar.Where(x => x.RefCatastral != null).Select(x => x.RefCatastral?.Trim()).Distinct());
        }

        /// <summary>
        /// GeoLocParcelasList.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="List{GeoLocParcela}"/>.</returns>
        public static List<GeoLocParcela> GeoLocParcelasList(string idUnidadCultivo, string idTemporada) {            
            List<GeoLocParcela> ret = new List<GeoLocParcela>();
            Database db = DB.ConexionOptiaqua;
            string sql = "SELECT dbo.Parcela.IdParcelaInt, dbo.Parcela.IdMunicipio, dbo.Parcela.IdPoligono, dbo.Parcela.IdParcela, Parcela.GEO.ToString() AS Geo, dbo.Municipio.Municipio, dbo.Parcela.GID ";
            sql += " FROM dbo.Parcela INNER JOIN dbo.UnidadCultivoParcela ON dbo.Parcela.IdParcelaInt = dbo.UnidadCultivoParcela.IdParcelaInt INNER JOIN ";
            sql += " dbo.Municipio ON dbo.Parcela.IdMunicipio = dbo.Municipio.IdMunicipio ";
            sql += "WHERE dbo.UnidadCultivoParcela.IdUnidadCultivo=@0 AND dbo.UnidadCultivoParcela.IdTemporada=@1 ";
            ret = db.Fetch<GeoLocParcela>(sql, idUnidadCultivo, idTemporada);
            return ret;
        }

        /// <summary>
        /// ParcelasList.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="object"/>.</returns>
        public static object IdParcelasList(string idUnidadCultivo, string idTemporada) {
            Database db = DB.ConexionOptiaqua;
            string sql = "SELECT DISTINCT dbo.Parcela.IdParcelaInt, dbo.Parcela.Descripcion, dbo.UnidadCultivoParcela.IdRegante, dbo.Parcela.SuperficieM2, dbo.UnidadCultivoParcela.IdUnidadCultivo ";
            sql += " FROM dbo.UnidadCultivoParcela INNER JOIN ";
            sql += " dbo.Parcela ON dbo.UnidadCultivoParcela.IdParcelaInt = dbo.Parcela.IdParcelaInt";
            sql += " WHERE dbo.UnidadCultivoParcela.IdUnidadCultivo = @0 AND dbo.UnidadCultivoParcela.IdTemporada=@1";
            return db.Fetch<object>(sql, idUnidadCultivo, idTemporada);
        }

        /// <summary>
        /// MunicipiosList.
        /// </summary>
        /// <returns><see cref="object"/>.</returns>
        public static object MunicipiosList() {
            Database db = DB.ConexionOptiaqua;
            string sql = "SELECT dbo.Municipio.IdMunicipio, dbo.Municipio.Municipio, dbo.Provincia.IdProvincia, dbo.Provincia.Provincia FROM dbo.Municipio INNER JOIN  dbo.Provincia ON dbo.Municipio.IdProvincia = dbo.Provincia.IdProvincia";
            return db.Fetch<object>(sql);
        }

        /// <summary>
        /// NParcelas.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="int?"/>.</returns>
        public static int? NParcelas(string idUnidadCultivo, string idTemporada) {
            Database db = DB.ConexionOptiaqua;
            string sql = "SELECT COUNT(IdParcelaInt) AS NParcelas FROM dbo.UnidadCultivoParcela GROUP BY IdUnidadCultivo, IdTemporada ";
            sql += " HAVING IdUnidadCultivo=@0 AND IdTemporada=@1";
            return db.SingleOrDefault<int?>(sql, idUnidadCultivo, idTemporada);
        }

        /// <summary>
        /// ObtenerMunicicioParaje.
        /// </summary>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="provincias">.</param>
        /// <param name="municipios">municipios<see cref="string"/>.</param>
        /// <param name="parajes">parajes<see cref="string"/>.</param>
        public static void ObtenerMunicicioParaje(string idTemporada, string idUnidadCultivo, out string provincias, out string municipios, out string parajes) {
            Database db = DB.ConexionOptiaqua;
            string sql = "Select Provincia,Municipio, Paraje from UnidadCultivoParaje where idTemporada=@0 and IdUnidadCultivo=@1";
            List<ProvinciaMunicipioParaje> lMunicipioProvinciaParaje = db.Fetch<ProvinciaMunicipioParaje>(sql, idTemporada, idUnidadCultivo);
            IEnumerable<string> lmunicicipos = lMunicipioProvinciaParaje.Select(x => x.Municipio).Distinct();
            IEnumerable<string> lParajes = lMunicipioProvinciaParaje.Select(x => x.Paraje).Distinct();
            IEnumerable<string> lProvincias = lMunicipioProvinciaParaje.Select(x => x.Provincia).Distinct();
            municipios = string.Join("#", lmunicicipos);
            parajes = string.Join("#", lParajes);
            provincias = string.Join("#", lProvincias);
        }

        /// <summary>
        /// ParajesList.
        /// </summary>
        /// <returns><see cref="object"/>.</returns>
        public static object ParajesList() {
            Database db = DB.ConexionOptiaqua;
            string sql = "Select * from ParajeAmpliado;";
            return db.Fetch<object>(sql);
        }

        /// <summary>
        /// Retorna una clase con todos los valores de la parcela IdParcela desde BD.
        /// </summary>
        /// <param name="idParcela">.</param>
        /// <returns>.</returns>
        public static ParcelaPoco Parcela(int idParcela) {
            Database db = null;
            ParcelaPoco ret = null;
            try {
                db = DB.ConexionOptiaqua;
                string sql = "SELECT IdParcelaInt, IdGadmin, IdRegante, IdProvincia, IdMunicipio, IdPoligono, IdParcela, IdParaje, Descripcion, Longitud, Latitud, XUTM, YUTM, Huso, Altitud, RefCatastral, GID, SuperficieM2 FROM dbo.Parcela";
                sql += " where idParcelaInt=" + idParcela.ToString();
                ret = db.Single<ParcelaPoco>(sql);
            } catch (Exception ex) {
                throw new Exception("No se pudo cargar parcela:" + idParcela.ToString() + " -" + ex.Message);
            }
            return ret;
        }

        internal static bool ParcelaExits(int idPar) {
            using (var db = DB.ConexionOptiaqua) {
                return db.Exists<ParcelaPoco>(idPar);
            }
        }

        internal static int? ParcelaIFromPMPP(int provincia, int municipio, int poligono, int parcela) {
            Database db = DB.ConexionOptiaqua;
            var ret = db.SingleOrDefault<int?>($"select IdParcelaInt from Parcela where IdProvincia={provincia} and IdMunicipio={municipio} and IdPoligono={poligono} and IdParcela={parcela}");
            return ret;
        }

        /// <summary>
        /// ParcelaList.
        /// </summary>
        /// <param name="IdTemporada">IdTemporada<see cref="string"/>.</param>
        /// <param name="IdParcela">IdParcela<see cref="string"/>.</param>
        /// <param name="IdRegante">IdRegante<see cref="string"/>.</param>
        /// <param name="IdMunicipio">IdMunicipio<see cref="string"/>.</param>
        /// <param name="Search">Search<see cref="string"/>.</param>
        /// <returns><see cref="object"/>.</returns>
        public static object ParcelaList(string IdTemporada, string IdParcela, string IdRegante, string IdMunicipio, string Search) {
            Database db = DB.ConexionOptiaqua;
            IdTemporada = IdTemporada.Quoted();
            Search = Search.Quoted();
            string sql = $"SELECT * FROM ParcelaList({IdTemporada},{IdParcela},{IdRegante},{IdMunicipio},{Search})";
            return db.Fetch<object>(sql);
        }

        /// <summary>
        /// ParcelasList.
        /// </summary>
        /// <returns><see cref="object"/>.</returns>
        public static object ParcelasList() {
            Database db = DB.ConexionOptiaqua;
            string sql;
            sql = "Select IdParcelaInt, IdRegante, Descripcion, SuperficieM2 from parcela";
            return db.Fetch<object>(sql);
        }

        internal static double? ParcelaSuperficieM2(int idPar) {
            using (var db = DB.ConexionOptiaqua) {
                return db.Single<double?>($"select SuperficieM2 from Parcela where IdParcelaInt={idPar}");
            }
        }

        internal static double ParcelaSuperficieM2(int idPar, Database db) {
            return db.Single<double?>($"select SuperficieM2 from Parcela where IdParcelaInt={idPar}") ?? 0;
        }

        /// <summary>
        /// ProvinciaList.
        /// </summary>
        /// <returns><see cref="object"/>.</returns>
        public static object ProvinciaList() {
            Database db = DB.ConexionOptiaqua;
            string sql = "Select * from Provincia;";
            return db.Fetch<object>(sql);
        }

        public class RefCatasPolPar {
            public string RefCatastral { get; set; }
            public string IdPoligono { get; set; }
            public string IdParcela { get; set; }
        }

        internal static List<itemTemporadaUC> UnidadCultivosDePacela(int idParcelaInt) {
            using (var db = DB.ConexionOptiaqua) {
                var ret = db.Fetch<itemTemporadaUC>($"select IdTemporada,IdUnidadCultivo as IdUC from UnidadCultivoParcela where IdParcelaInt={idParcelaInt}");
                return ret;
            }
        }
    }
}
