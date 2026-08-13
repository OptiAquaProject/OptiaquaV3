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
    /// Unidades de cultivo: listados y filtros, cultivo asignado por temporada,
    /// superficie y coste del metro cúbico de agua.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        /// <summary>
        /// The ListaUnidadesCultivoQueCumplenFiltro.
        /// </summary>
        /// <param name="idMunicipio">The idMunicipio<see cref="int?"/>.</param>
        /// <param name="idCultivo">The idCultivo<see cref="string"/>.</param>
        /// <param name="idRegante">The idRegante<see cref="int?"/>.</param>
        /// <returns>The <see cref="List{string}"/>.</returns>
        internal static List<string> ListaUnidadesCultivoQueCumplenFiltro(int? idMunicipio, string idCultivo, int? idRegante) {
            Database db = DB.ConexionOptiaqua;
            string sql = "SELECT Distinct IdUnidadCultivo from FiltroParcelasDatosHidricos ";
            string filtro = " Where ";
            if (idMunicipio != null) {
                sql += filtro + "idMunicipio=" + idMunicipio.ToString();
                filtro = " and ";
            }

            if (idCultivo.Unquoted() != "") {
                sql += filtro + " IdCultivo=" + idCultivo;
                filtro = " and ";
            }

            if (idRegante != null)
                sql += filtro + "IdRegante=" + idRegante.ToString();

            return db.Fetch<string>(sql);
        }

        /// <summary>
        /// UnidadCultivo.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <returns><see cref="UnidadCultivo"/>.</returns>
        public static UnidadCultivo UnidadCultivo(string idUnidadCultivo) {
            if (idUnidadCultivo == null)
                return null;
            Database db = DB.ConexionOptiaqua;
            return db.SingleOrDefaultById<UnidadCultivo>(idUnidadCultivo);
        }

        /// <summary>
        /// Retorna los datos de la tabla ParcelasCultivo.
        /// </summary>
        /// <param name="idUnidadCultivo">.</param>
        /// <param name="idTemporada">.</param>
        /// <returns>.</returns>
        public static UnidadCultivoCultivo UnidadCultivoCultivo(string idUnidadCultivo, string idTemporada) {
            if (idUnidadCultivo == null || idTemporada == null)
                return null;
            Database db = DB.ConexionOptiaqua;
            string sql;
            sql = "Select * from UnidadCultivoCultivo where idUnidadCultivo=@0 AND IdTemporada=@1";
            return db.SingleOrDefault<UnidadCultivoCultivo>(sql, idUnidadCultivo, idTemporada);
        }

        /// <summary>
        /// UnidadCultivoCultivoTemporadaSave.
        /// </summary>
        /// <param name="IdUnidadCultivo">IdUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <param name="idCultivo">idCultivo<see cref="int"/>.</param>
        /// <param name="idRegante">idRegante<see cref="int"/>.</param>
        /// <param name="idTipoRiego">idTipoRiego<see cref="int"/>.</param>
        /// <param name="fechaSiembra">fechaSiembra<see cref="string"/>.</param>
        public static void UnidadCultivoCultivoTemporadaSave(string IdUnidadCultivo, string idTemporada, int idCultivo, int idRegante, int idTipoRiego, string fechaSiembra) {
            Database db = null;
            try {
                db = DB.ConexionOptiaqua;
                db.BeginTransaction();
                if (DateTime.TryParse(fechaSiembra, out DateTime fs) == false) {
                    throw new Exception("Error. La fecha de siembra no es correcta. ");
                }

                // validar Unidad de cultivo                
                if (db.Exists<UnidadCultivo>(IdUnidadCultivo) == false) {
                    throw new Exception("Error. No existe la unida de cultivo indicada.");
                }

                // validar Cultivo
                if (db.Exists<Cultivo>(idCultivo) == false) {
                    throw new Exception("Error. No existe el cultivo indicado. ");
                }

                // validar Regante
                if (db.Exists<Regante>(idRegante) == false) {
                    throw new Exception("Error. No existe el Regante indicado. ");
                }

                // validar TipoRiego
                if (db.Exists<RiegoTipo>(idTipoRiego) == false) {
                    throw new Exception("Error. No existe el tipo de Riego indicado.");
                }

                //Si existe, se elimina
                db.Execute(" delete from UnidadCultivoCultivo where IdUnidadCultivo=@0 and IdTemporada=@1 and IdCultivo=@2 ", IdUnidadCultivo, idTemporada, idCultivo);

                // Crear Registro Parcelas Cultivo
                UnidadCultivoCultivo uniCulCul = new UnidadCultivoCultivo {
                    IdUnidadCultivo = IdUnidadCultivo,
                    IdCultivo = idCultivo,
                    IdRegante = idRegante,
                    IdTemporada = idTemporada,
                    IdTipoRiego = idTipoRiego,
                    Pluviometria = PluviometriaTipica(idTipoRiego)
                };
                db.Insert(uniCulCul);

                // Leer Cultivo Etapas de IdCultivo
                List<CultivoEtapas> listaCF = db.Fetch<CultivoEtapas>("Select * from CultivoEtapas Where IdCultivo=@0", idCultivo);
                if (listaCF.Count == 0) {
                    throw new Exception("Error. No existe una definición de las Etapas para el cultivo indicado.");
                }

                DateTime fechaEtapa = fs;
                foreach (CultivoEtapas cf in listaCF) {
                    UnidadCultivoCultivoEtapas pcf = new UnidadCultivoCultivoEtapas {
                        IdUnidadCultivo = uniCulCul.IdUnidadCultivo,
                        IdTemporada = uniCulCul.IdTemporada,
                        IdEtapaCultivo = cf.OrdenEtapa,
                        Etapa = cf.Etapa,
                        FechaInicioEtapa = fechaEtapa
                    };
                    fechaEtapa = fechaEtapa.AddDays(cf.DuracionDiasEtapa);
                    pcf.FechaInicioEtapaConfirmada = null;
                    pcf.DefinicionPorDias = cf.DefinicionPorDias;
                    pcf.KcInicial = cf.KcInicial;
                    pcf.KcFinal = cf.KcFinal;
                    pcf.CobInicial = cf.CobInicial;
                    pcf.CobFinal = cf.CobFinal;
                    pcf.FactorDeAgotamiento = cf.FactorAgotamiento;
                    db.Insert(pcf);
                }

                db.CompleteTransaction();
                return;
            } catch (Exception ex) {
                db.AbortTransaction();
                throw new Exception("Error. No existe una definición de las Etapas para el cultivo indicado." + ex.Message);
            }
        }

        /// <summary>
        /// UnidadCultivoDatosAmpliados.
        /// </summary>
        /// <param name="fecha"></param>
        /// <param name="idUnidadCultivoFiltro"></param>        
        /// <returns><see cref="object"/>.</returns>
        public static List<UnidadCultivoDatosAmpliados> UnidadCultivoDatosAmpliados(DateTime fecha, string idUnidadCultivoFiltro) {
            Database db = DB.ConexionOptiaqua;
            string filtro = "";
            if (!string.IsNullOrWhiteSpace(idUnidadCultivoFiltro))
                filtro += $" WHERE IdUnidadCultivo='{idUnidadCultivoFiltro}' ";

            string sql = $"SELECT * FROM UnidadCultivoDatosAmpliados " + filtro;
            List<UnidadCultivoDatosAmpliados> ret = db.Fetch<UnidadCultivoDatosAmpliados>(sql);
            foreach (UnidadCultivoDatosAmpliados dat in ret) {
                string idTemporada = DB.TemporadaDeFecha(dat.IdUnidadCultivo, fecha);
                if (string.IsNullOrWhiteSpace(idTemporada) || idTemporada != dat.IdTemporada) {
                    dat.IdTemporada = null;
                    continue;
                }
                ObtenerMunicicioParaje(idTemporada, dat.IdUnidadCultivo, out string provincias, out string municipios, out string parajes);
                dat.Provincia = provincias;
                dat.Municipio = municipios;
                dat.Paraje = parajes;
                dat.FechaSiembra = DB.FechaSiembra(dat.IdUnidadCultivo, idTemporada);
                dat.Hidrantes = DB.HidrantesListJson(dat.IdUnidadCultivo, idTemporada);
                DatosParcelasList(dat.IdUnidadCultivo, idTemporada, out string poligonos, out string parcelas, out string refCatastrales);
                dat.Parcelas = parcelas;
                dat.Poligonos = poligonos;
                dat.RefCatastrales = refCatastrales;
                dat.IdEstacion = EstacionDeUC(dat.IdUnidadCultivo, idTemporada);
            }
            ret.RemoveAll(x => x.IdTemporada == null);
            return ret;
        }

        internal static List<string> UnidadCultivoDelete(string lIdUnidadCultivos, string idTemporada) {
            var ret = new List<string>();
            var db = DB.ConexionOptiaqua;
            var temporada = db.SingleOrDefaultById<Temporada>(idTemporada);
            if (temporada == null) {
                ret.Add("No se encontrol temporada");
                return ret;
            }
            var lUniCul = lIdUnidadCultivos.Split(',').ToList();
            if (lUniCul.Count == 0) {
                ret.Add("No se han indicado unididades de cultivo");
                return ret;
            }

            foreach (var iuc in lUniCul) {
                var ucc = new UnidadCultivoCultivo {
                    IdUnidadCultivo = iuc,
                    IdTemporada = idTemporada
                };
                var n1 = db.Delete<UnidadCultivoDatosExtra>("where IdUnidadCultivo=@0 and fecha between @1 and @2", iuc, temporada.FechaInicial, temporada.FechaFinal);
                var nDel = db.Delete(ucc);
                if (nDel == 1) {
                    ret.Add($"Eliminada UC: {iuc}");
                } else {
                    ret.Add($"No se puedo eliminar UC: {iuc}");
                }
            }
            return ret;
        }

        /// <summary>
        /// UnidadCultivoExtensionM2.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="float"/>.</returns>
        public static double UnidadCultivoExtensionM2(string idUnidadCultivo, string idTemporada) {
            if (idUnidadCultivo == null || idTemporada == null)
                return 0;
            double? ret = 0;
            Database db = DB.ConexionOptiaqua;
            string sql = "Select SuperficieM2 From UnidadCultivoCultivo Where IdUnidadCultivo=@0 and IdTemporada=@1";
            ret = db.SingleOrDefault<float?>(sql, idUnidadCultivo, idTemporada);
            if (ret != null && (double)ret != 0)
                return (double)ret;
            sql = " SELECT TOP(1) SuperficieM2 FROM UnidadCultivoCultivo WHERE(IdTemporada = @0) ORDER BY IdTemporada DESC";
            ret = db.SingleOrDefault<float?>(sql, idUnidadCultivo);
            if (ret != null)
                return (double)ret;

            sql = "  SELECT SUM(dbo.Parcela.SuperficieM2) AS Suma ";
            sql += " FROM dbo.UnidadCultivoParcela INNER JOIN ";
            sql += " dbo.Parcela ON dbo.UnidadCultivoParcela.IdParcelaInt = dbo.Parcela.IdParcelaInt ";
            sql += " GROUP BY dbo.UnidadCultivoParcela.IdUnidadCultivo, dbo.UnidadCultivoParcela.IdTemporada ";
            sql += " HAVING(dbo.UnidadCultivoParcela.IdUnidadCultivo =@0) AND(dbo.UnidadCultivoParcela.IdTemporada =@1)";
            ret = db.SingleOrDefault<double?>(sql, idUnidadCultivo, idTemporada);
            if (ret != null && ret != 0) {
                db.Execute($"update UnidadCultivoCultivo set SuperficieM2={ret} where IdUnidadCultivo='{idUnidadCultivo}' and IdTemporada='{idTemporada}'");
            }

            return ret ?? 0;
        }

        /// <summary>
        /// 
        /// UnidadCultivoList
        /// </summary>
        /// <param name="fecha"></param>
        /// <param name="idUnidadCultivo"></param>
        /// <param name="idRegante"></param>
        /// <param name="idCultivo"></param>
        /// <param name="idMunicipio"></param>
        /// <param name="idTipoRiego"></param>
        /// <param name="idPoligono"></param>
        /// <param name="idParcela"></param>
        /// <param name="search"></param>
        /// <param name="idUsuario"></param>
        /// <param name="role"></param>
        /// <returns></returns>

        public static object UnidadCultivoList(DateTime fecha, string idUnidadCultivo, string idRegante, string idCultivo, string idMunicipio, string idTipoRiego, string idPoligono, string idParcela, string search, int idUsuario, string role) {
            var db= DB.ConexionOptiaqua;
            idUnidadCultivo = idUnidadCultivo.Quoted();
            search = search.Quoted();
            string sql = $"SELECT * FROM UnidadcultivoList('{fecha:dd/MM/yyyy}','',{idUnidadCultivo},{idRegante},{idCultivo},{idMunicipio},{idTipoRiego},{search})";
            
            idPoligono = idPoligono.Unquoted();
            idParcela = idParcela.Unquoted();
            List<Dictionary<string, object>> lRet = db.Fetch<Dictionary<string, object>>(sql);
            List<Dictionary<string, object>> lValidos = new List<Dictionary<string, object>>();
            List<string> lAsesor = new List<string>();
            if (role == "asesor")
                lAsesor = DB.AsesorUnidadCultivoList(idUsuario);
            foreach (Dictionary<string, object> dic in lRet) {
                string idUC = dic["IdUnidadCultivo"] as string;
                var idTemporada = dic["IdTemporada"] as string;
                if (role == "asesor") {
                    if (!lAsesor.Contains(idUC))
                        continue;

                } else if (role == "dbo") {
                    if (DB.LaUnidadDeCultivoPerteneceAlReganteEnLaTemporada(idUC, idUsuario, idTemporada) == false)
                        continue;
                }
                DB.DatosParcelasList(idUC, idTemporada, out string poligonos, out string parcelas, out string refCastrales);
                if (!string.IsNullOrWhiteSpace(idPoligono)) {
                    if (!poligonos.Split('#').Contains(idPoligono))
                        continue;
                }
                if (!string.IsNullOrWhiteSpace(idParcela)) {
                    if (!parcelas.Split('#').Contains(idParcela))
                        continue;
                }
                dic.Add("SuperficieM2", UnidadCultivoExtensionM2(idUC, idTemporada));
                dic.Add("FechaSiembra", DB.FechaSiembra(idUC, idTemporada));
                dic.Add("IdParcelas:", parcelas);
                dic.Add("IdPoligonos:", poligonos);
                dic.Add("HidranteTomaJson", DB.HidrantesListJson(idUC, idTemporada));
                lValidos.Add(dic);
                List<GeoLocParcela> lGeoLocParcelas = DB.GeoLocParcelasList(idUC, idTemporada);
                string geo = Newtonsoft.Json.JsonConvert.SerializeObject(lGeoLocParcelas);
                dic.Add("GeoLocJson", geo);
            }
            return lValidos;
        }

        /// <summary>
        /// UnidadCultivoList.
        /// </summary>
        /// <returns><see cref="List{UnidadCultivo}"/>.</returns>
        public static List<UnidadCultivo> UnidadCultivoList() {
            Database db = DB.ConexionOptiaqua;
            return db.Fetch<UnidadCultivo>();
        }

        /// <summary>
        /// UnidadCultivoList.
        /// </summary>
        /// <param name="idRegante">idRegante<see cref="int"/>.</param>
        /// <returns><see cref="List{string}"/>.</returns>
        public static List<string> UnidadCultivoList(int idRegante) {
            try {
                Database db = DB.ConexionOptiaqua;
                string sql;
                sql = "Select Distinct IdUnidadCultivo from UnidadcultivoCultivo where IdRegante=@0";
                return db.Fetch<string>(sql, idRegante);
            } catch {
                string msgErr = "No se pudo cargar lista de parcelas para los parámetros:\n";
                msgErr += "IdRegante:" + idRegante.ToString() + "\n";
                throw new Exception(msgErr);
            }
        }

        /// <summary>
        /// UnidadCultivoTemporadaCosteM3Agua.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="double?"/>.</returns>
        public static double UnidadCultivoTemporadaCosteM3Agua(string idUnidadCultivo, string idTemporada) {
            if (string.IsNullOrEmpty(idTemporada) || string.IsNullOrWhiteSpace(idUnidadCultivo))
                return 0;
            Database db = DB.ConexionOptiaqua;
            string sql = "Select CosteM3Agua from UnidadCultivoCultivo where idUnidadCultivo=@0  and IdTemporada=@1;";
            double? ret = db.SingleOrDefault<double?>(sql, idUnidadCultivo, idTemporada);
            if (ret == null) {
                sql = "Select CosteM3Agua from Temporada where IdTemporada=@0;";
                ret = db.FirstOrDefault<double?>(sql, idTemporada);
            }
            return ret ?? 0;
        }

        /// <summary>
        /// UnidadCultivoTemporadaCosteM3AguaSave.
        /// </summary>
        /// <param name="param">param<see cref="ParamPostCosteM3Agua"/>.</param>
        /// <returns><see cref="object"/>.</returns>
        public static object UnidadCultivoTemporadaCosteM3AguaSave(ParamPostCosteM3Agua param) {
            if (string.IsNullOrWhiteSpace(param.IdUnidadCultivo) || string.IsNullOrWhiteSpace(param.IdTemporada))
                return "Error en actualización";
            Database db = DB.ConexionOptiaqua;
            if (param.CosteM3Agua <= 0)
                param.CosteM3Agua = null;
            UnidadCultivoCultivo ucc = db.SingleOrDefault<UnidadCultivoCultivo>("where IdUnidadCultivo=@0 AND IdTemporada=@1", param.IdUnidadCultivo, param.IdTemporada);
            if (ucc != null) {
                ucc.CosteM3Agua = param.CosteM3Agua;
            }
            db.Save(ucc);
            return "OK";
        }

        /// <summary>
        /// UnidadesCultivoList.
        /// </summary>
        /// <param name="idRegante">idRegante<see cref="int"/>.</param>
        /// <param name="fecha">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="List{string}"/>.</returns>
        public static List<string> UnidadesCultivoList(int idRegante, DateTime fecha) {
            List<string> ret = new List<string>();
            try {
                if (fecha == null)
                    return new List<string>();
                List<string> lTemporadas = DB.TemporadasDeFecha(fecha);
                Database db = DB.ConexionOptiaqua;
                string sql = "Select Distinct IdUnidadCultivo from UnidadCultivoCultivo where IdRegante=@0 AND IdTemporada =@1";
                foreach (string idTemporada in lTemporadas) {
                    List<string> deTemporada = db.Fetch<string>(sql, idRegante, idTemporada);
                    ret.AddRange(deTemporada);
                }

            } catch {
                string msgErr = "No se pudo cargar lista de parcelas para los parámetros:\n";
                msgErr += "IdRegante:" + idRegante.ToString() + "\n";
                msgErr += "fecha:" + fecha.ToString() + "\n";
                throw new Exception(msgErr);
            }
            return ret;
        }

        /// <summary>
        /// UnidadesDeCultivoList
        /// </summary>
        /// <param name="lTemporadas"></param>
        /// <param name="idUsuario"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        public static object UnidadesDeCultivoList(List<string> lTemporadas, int idUsuario, string role) {
            List<UnidadCultivoConSuperficieYGeoLoc> ret = new List<UnidadCultivoConSuperficieYGeoLoc>();
            Database db = DB.ConexionOptiaqua;
            foreach (string idTemporada in lTemporadas) {
                string sql = "Select DISTINCT IdUnidadCultivo, IdParcelaInt, IdRegante from UnidadCultivoParcela where IdTemporada=@0 ";
                List<UnidadCultivoConSuperficieYGeoLoc> deUnaTemporada = db.Fetch<UnidadCultivoConSuperficieYGeoLoc>(sql, idTemporada);
                foreach (UnidadCultivoConSuperficieYGeoLoc item in deUnaTemporada) {
                    if (!DB.EstaAutorizado(idUsuario, role, item.IdUnidadCultivo))
                        continue;
                    item.Hidrantes = DB.HidrantesListJson(item.IdUnidadCultivo, idTemporada);
                    item.SuperficieM2 = UnidadCultivoExtensionM2(item.IdUnidadCultivo, idTemporada);
                    List<GeoLocParcela> lGeoLocParcelas = DB.GeoLocParcelasList(item.IdUnidadCultivo, idTemporada);
                    item.GeoLocJson = Newtonsoft.Json.JsonConvert.SerializeObject(lGeoLocParcelas);
                    ret.Add(item);
                }
            }
            return ret;
        }
    }
}
