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
    /// Suelos: horizontes, texturas, elementos gruesos y materia orgánica, mapas
    /// de suelo y los valores por defecto de La Rioja.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        private static List<double> CalculaHorizontes(List<DatosSueloDB> lDS) {
            var ret = new List<double>();
            bool yaTenemospMax = false;
            foreach (var ds in lDS) {
                var lResto = lDS
                    .Where(x => x.Nivel < ds.Nivel)
                    .OrderByDescending(x => x.Nivel)
                    .ToList();
                var p = ds.ProfundidadCMFinal.Value;
                foreach (var r in lResto) {
                    var minP = Math.Min(r.ProfundidadCMFinal.Value, p);
                    if (ds.Arena == null && r.Arena != null) {
                        p = minP;
                    }
                    if (ds.Arcilla == null && r.Arcilla != null) {
                        p = minP;
                    }
                    if (ds.Limo == null && r.Limo != null) {
                        p = minP;
                    }
                    if (ds.MateriaOrganica == null && r.MateriaOrganica != null) {
                        p = minP;
                    }
                    if (ds.ElementosGruesos == null) {
                        p = minP;
                    }
                }
                if (ds.EsHorizonteControl == false || (ds.EsHorizonteControl == true && yaTenemospMax == false)) {
                    ret.Add(p);// añadir horizonte 
                    if (ds.EsHorizonteControl == true && yaTenemospMax == false)
                        yaTenemospMax = true;
                }
            }
            return ret;
        }

        private static double? DatosElemtosGrusosEnHorizonte(List<DatosSueloDB> lDS, double h) {
            var lCubren = lDS
                .Where(s =>
                    s.ProfundidadCMInicial < h && s.ProfundidadCMFinal >= h
                    && s.ElementosGruesos != null)
                .ToList();
            var listsPreferencia = lCubren.OrderByDescending(x => x.Nivel).ToList();
            var ret = listsPreferencia.FirstOrDefault()?.ElementosGruesos;
            return ret;
        }

        private static double? DatosMateriaOrganicaEnHorizonte(List<DatosSueloDB> lDS, double h) {
            var lCubren = lDS
                .Where(s =>
                    s.ProfundidadCMInicial < h && s.ProfundidadCMFinal >= h
                    && s.MateriaOrganica != null)
                .ToList();
            var listsPreferencia = lCubren.OrderByDescending(x => x.Nivel).ToList();
            var ret = listsPreferencia.FirstOrDefault()?.MateriaOrganica;
            return ret;
        }

        public static List<DatosSuelo> DatosSueloBaseNew(string idVersionMapa, int idParcelaInt, double? lat, double? lng, string refCatastral) {
            Database db = DB.ConexionOptiaqua;

            if (lat == 0 || lng == 0 || lat == null || lng == null) {
                InsertaEvento($"La Parcela {idParcelaInt} no tiene valores Lat-Lng");
                return null;
            }

            var latStr = lat.Value.ToString().Replace(",", ".");
            var lngStr = lng.Value.ToString().Replace(",", ".");

            var sqlBase = @"
                    SELECT  Nivel, IdVersion,ID, IdMapaSuelo,
                            HS_ESPESOR_cm, HS_ARENA_Porc, HS_ARCILLA_Porc, HS_LIMO_Porc, HS_EGRUESO_Porc,  HS_MATORG_Porc, HS_TEXTURA,
                            SC_ESPESOR_cm, SC_ARENA_Porc, SC_ARCILLA_Porc, SC_LIMO_Porc, SC_EGRUESO_Porc, SC_MATORG_Porc, PROF_EFECTIVA_cm 
                    FROM dbo.MapaSuelo
                 ";
            sqlBase += $" where IdVersion='{idVersionMapa}' and  geom.STContains( geometry::STGeomFromText('POINT({lngStr}  {latStr})', 4326)) =1 order by Nivel desc";
            var lSuelosDB = db.Fetch<MapaSueloPoco>(sqlBase);
            var lDS = new List<DatosSueloDB>();
            foreach (var s in lSuelosDB) {
                var dsHS = new DatosSueloDB {
                    IdParcelaInt = idParcelaInt,
                    Arcilla = s.HS_ARCILLA_Porc / 100.0,
                    Arena = s.HS_ARENA_Porc / 100.0,
                    Limo = s.HS_LIMO_Porc / 100.0,
                    MateriaOrganica = s.HS_MATORG_Porc / 100.0,
                    ElementosGruesos = s.HS_EGRUESO_Porc / 100.0,
                    ProfundidadCMInicial = 0,
                    ProfundidadCMFinal = s.HS_ESPESOR_cm,
                    Nivel = s.Nivel,
                    EsHorizonteControl = false,
                };
                var dsSC = new DatosSueloDB {
                    IdParcelaInt = idParcelaInt,
                    Arcilla = s.SC_ARCILLA_Porc / 100.0,
                    Arena = s.SC_ARENA_Porc / 100.0,
                    Limo = s.SC_LIMO_Porc / 100.0,
                    MateriaOrganica = s.SC_MATORG_Porc / 100.0,
                    ElementosGruesos = s.SC_EGRUESO_Porc / 100.0,
                    ProfundidadCMInicial = s.HS_ESPESOR_cm,
                    ProfundidadCMFinal = s.PROF_EFECTIVA_cm,
                    Nivel = s.Nivel,
                    EsHorizonteControl = true,
                };
                if (dsHS.ProfundidadCMFinal != null) // si no tenemos un valor de profundidad no vale para nada 
                    lDS.Add(dsHS);
                if (dsSC.ProfundidadCMFinal != null)// si no tenemos un valor de profundidad no vale para nada 
                    lDS.Add(dsSC);
            }

            var lH = CalculaHorizontes(lDS).Distinct().OrderBy(x => x).ToList();
            var lRet = new List<DatosSuelo>();
            foreach (var h in lH) {
                var datosTextura = DatosTexturaEnHorizonte(lDS, h);
                var eg = DatosElemtosGrusosEnHorizonte(lDS, h);
                var mo = DatosMateriaOrganicaEnHorizonte(lDS, h);
                var ds = new DatosSuelo {
                    Arcilla = datosTextura.Arcilla.Value,
                    Arena = datosTextura.Arena.Value,
                    Limo = datosTextura.Limo.Value,
                    IdParcelaInt = datosTextura.IdParcelaInt,
                    ElementosGruesos = eg.Value,
                    MateriaOrganica = mo.Value,
                    //Nivel = datosTextura.Nivel,
                    ProfundidadCM = h
                };
                lRet.Add(ds);
            }
            if (lRet.Count == 0)
                lRet = new List<DatosSuelo>();
            return lRet;
        }

        public static List<DatosSuelo> DatosSueloComunidadRegantes(string idUC, string idTemporada) {

            Database db = DB.ConexionOptiaqua;
            var sql = $@"
                SELECT dbo.ParcelaSuelo.*, dbo.Parcela.SuperficieM2 as Superficie FROM dbo.Parcela INNER JOIN
                    dbo.ParcelaSuelo ON dbo.Parcela.IdParcelaInt = dbo.ParcelaSuelo.IdParcelaInt INNER JOIN
                    dbo.UnidadCultivoParcela ON dbo.Parcela.IdParcelaInt = dbo.UnidadCultivoParcela.IdParcelaInt
                WHERE (dbo.UnidadCultivoParcela.IdTemporada = '{idTemporada}') AND (dbo.UnidadCultivoParcela.IdUnidadCultivo = '{idUC}')            
            ";

            var lDatosSueloParcelas = db.Fetch<DatosSuelo>(sql);
            if (lDatosSueloParcelas.Count == 0)
                return lDatosSueloParcelas;
            double hAnt = 0;
            var porParcela = lDatosSueloParcelas.GroupBy(x => x.IdParcelaInt).Select(x => new { x.Key, list = x.ToList() }).ToList();
            // EN ldsBase ahora tenemos datos del suelo para todas las parcelas. Si lo datos se han tomado de MapaBese con dos horizontes, sino con un solo horizonte.
            // Ahora lo tenemos que fusionar en lista única.
            var ret = new List<DatosSuelo>();
            double totalSuperficie = porParcela.Sum(x => x.list[0].Superficie);
            if (totalSuperficie == 0)
                totalSuperficie = 1;
            var hEstudio = lDatosSueloParcelas.Where(x => x.ProfundidadCM > hAnt).Min(x => x.ProfundidadCM);
            while (hEstudio > 0) {
                double arena_dividendo = 0, limo_dividendo = 0, arcilla_dividendo = 0, eg_dividendo = 0, mo_dividendo = 0;
                foreach (var par in porParcela) {
                    var ds = par.list.FirstOrDefault(x => x.ProfundidadCM <= hEstudio);
                    if (ds != null) {
                        arena_dividendo += ds.Arena * ds.Superficie;
                        limo_dividendo += ds.Limo * ds.Superficie;
                        arcilla_dividendo += ds.Arcilla * ds.Superficie;
                        eg_dividendo += ds.ElementosGruesos * ds.Superficie;
                        mo_dividendo += ds.MateriaOrganica * ds.Superficie;
                    }
                }
                var reg = new DatosSuelo {
                    ProfundidadCM = hEstudio,
                    Superficie = totalSuperficie,
                    Arena = arena_dividendo / totalSuperficie,
                    Limo = limo_dividendo / totalSuperficie,
                    Arcilla = arcilla_dividendo / totalSuperficie,
                    ElementosGruesos = eg_dividendo / totalSuperficie,
                    MateriaOrganica = mo_dividendo / totalSuperficie,
                    IdParcelaInt = -1,
                    //IdVersion="-datos-extra"                    
                };
                ret.Add(reg);
                hAnt = hEstudio;
                var lSigH = lDatosSueloParcelas.Where(x => x.ProfundidadCM > hAnt);
                if (lSigH.Any())
                    hEstudio = lSigH.Min(x => x.ProfundidadCM);
                else
                    hEstudio = 0;
            }
            return ret;
        }

        private static DatosSueloDB DatosTexturaEnHorizonte(List<DatosSueloDB> lDS, double h) {
            var lCubren = lDS
                .Where(s =>
                    s.ProfundidadCMInicial < h && s.ProfundidadCMFinal >= h
                    && s.MateriaOrganica != null
                    && s.Limo != null
                    && s.Arcilla != null
                    && s.Arena != null)
                .ToList();
            var listsPreferencia = lCubren.OrderByDescending(x => x.Nivel).ToList();
            var ret = listsPreferencia.FirstOrDefault();
            return ret;
        }

        /// <summary>
        /// The ElementosGruesosTipo.
        /// </summary>
        /// <param name="IdElementosGruesos">The IdElementosGruesos<see cref="string"/>.</param>
        /// <returns>The <see cref="ElementosGruesosTipo"/>.</returns>
        public static ElementosGruesosTipo ElementosGruesosTipo(string IdElementosGruesos) {
            Database db = DB.ConexionOptiaqua;
            ElementosGruesosTipo ret = db.SingleOrDefaultById<ElementosGruesosTipo>(IdElementosGruesos);
            if (ret == null)
                throw new Exception();
            return ret;
        }

        /// <summary>
        /// ElementosGruesosTipo.
        /// </summary>
        /// <returns><see cref="List{ElementosGruesosTipo}"/>.</returns>
        public static List<ElementosGruesosTipo> ElementosGruesosTipoList() {
            Database db = DB.ConexionOptiaqua;
            List<ElementosGruesosTipo> ret = db.Fetch<ElementosGruesosTipo>("select * from ElementosGruesosTipo");
            return ret;
        }

        /// <summary>
        /// The MateriaOrganicaTipo.
        /// </summary>
        /// <param name="idMateriaOrganicaTipo">The idMateriaOrganicaTipo<see cref="string"/>.</param>
        /// <returns>The <see cref="MateriaOrganicaTipo"/>.</returns>
        public static MateriaOrganicaTipo MateriaOrganicaTipo(string idMateriaOrganicaTipo) {
            Database db = DB.ConexionOptiaqua;
            MateriaOrganicaTipo ret = db.SingleById<MateriaOrganicaTipo>(idMateriaOrganicaTipo);
            return ret;
        }

        /// <summary>
        /// MateriaOrganicaTipo.
        /// </summary>
        /// <returns><see cref="List{MateriaOrganicaTipo}"/>.</returns>
        public static List<MateriaOrganicaTipo> MateriaOrganicaTipoList() {
            Database db = DB.ConexionOptiaqua;
            List<MateriaOrganicaTipo> ret = db.Fetch<MateriaOrganicaTipo>("select * from MateriaOrganicaTipo");
            return ret;
        }

        internal static List<DatosSuelo> SueloUnidadCultivoTemporada(string idUnidadCultivo, string idTemporada) {
            using (var db = DB.ConexionOptiaqua) {
                var ret = db.Fetch<DatosSuelo>($"Select * from SueloUnidadCultivoTemporada where IdUC='{idUnidadCultivo}'  and IdTemporada='{idTemporada}' ");
                return ret;
            }
        }

        public static List<DatosSuelo> UnidadCultivoDatosExtraSuelo(string idUC, string idTemporada) {
            var ret = DB.ConexionOptiaqua.Fetch<DatosSuelo>("select * from UnidadCultivoDatosExtraSuelo where IdUnidadCultivo=@0 and IdTemporada=@1", idUC, idTemporada);
            return ret;
        }

        public static List<DatosSuelo> UnidadCultivoSueloListNew(string idUC, string idTemporada) {

            var lDatosExtra = UnidadCultivoDatosExtraSuelo(idUC, idTemporada);
            if (lDatosExtra != null && lDatosExtra.Count > 0)
                return lDatosExtra;

            var lComunidadRegantes = DatosSueloComunidadRegantes(idUC, idTemporada);
            // si tenemos datos de la unidad de cultivo 
            if (lComunidadRegantes != null && lComunidadRegantes.Count > 0)
                return lComunidadRegantes;

            var db = DB.ConexionOptiaqua;
            var lDatosSueloParcelas = new List<DatosSuelo>();
            var idVersionMapa = db.Single<string>($"select IdVersionMapa from Temporada where idTemporada='{idTemporada}'");
            if (idVersionMapa != null) {
                var sqlParcelas = $"select * from ParcelasDeUC where IdTemporada='{idTemporada}' and IdUnidadCultivo='{idUC}'";
                var lParcelas = db.Fetch<ParcelaPoco>(sqlParcelas);
                foreach (var parcela in lParcelas) {
                    var lDsBase = DatosSueloBaseNew(idVersionMapa, parcela.IdParcelaInt, parcela.Latitud, parcela.Longitud, parcela.RefCatastral);
                    if (lDsBase != null) {
                        lDatosSueloParcelas.AddRange(lDsBase);
                    }
                }
            }
            if (lDatosSueloParcelas.Count == 0)
                throw new Exception($"No se encontraron datos de suelo para la unidad de cultivo: '{idUC}' en la temporada: '{idTemporada}'");


            var porParcela = lDatosSueloParcelas.
                GroupBy(x => new { x.IdParcelaInt, superficie = ParcelaSuperficieM2(x.IdParcelaInt, db) }).
                Select(x => new { x.Key, list = x.ToList() }).ToList();
            double totalSuperficie = porParcela.Sum(x => x.Key.superficie);

            // EN ldsBase ahora tenemos datos del suelo para todas las parcelas.
            // Ahora lo tenemos que fusionar en lista única.
            var ret = new List<DatosSuelo>();
            double hAnt = 0;
            if (porParcela.Count == 0)
                throw new Exception($"No se encontraron datos de suelo para la unidad de cultivo: '{idUC}' en la temporada: '{idTemporada}'");

            var hEstudio = lDatosSueloParcelas.Where(x => x.ProfundidadCM > hAnt).Min(x => x.ProfundidadCM);
            while (hEstudio > 0) {
                double arena_dividendo = 0, limo_dividendo = 0, arcilla_dividendo = 0, eg_dividendo = 0, mo_dividendo = 0;
                double totalSuperfieConDatosEnHorizonte = 0;
                foreach (var par in porParcela) {
                    var ds = par.list.Where(x => x.ProfundidadCM >= hEstudio).OrderBy(x => x.ProfundidadCM).FirstOrDefault();
                    if (ds != null) {
                        arena_dividendo += ds.Arena * par.Key.superficie;
                        limo_dividendo += ds.Limo * par.Key.superficie;
                        arcilla_dividendo += ds.Arcilla * par.Key.superficie;
                        eg_dividendo += ds.ElementosGruesos * par.Key.superficie;
                        mo_dividendo += ds.MateriaOrganica * par.Key.superficie;
                    }
                    totalSuperfieConDatosEnHorizonte += par.Key.superficie;
                }
                // Efitar división por cero.En este caso todos los valores será cero
                if (totalSuperfieConDatosEnHorizonte == 0)
                    totalSuperfieConDatosEnHorizonte = 1;
                var reg = new DatosSuelo {
                    ProfundidadCM = hEstudio,
                    Superficie = totalSuperficie,
                    Arena = arena_dividendo / totalSuperfieConDatosEnHorizonte,
                    Limo = limo_dividendo / totalSuperfieConDatosEnHorizonte,
                    Arcilla = arcilla_dividendo / totalSuperfieConDatosEnHorizonte,
                    ElementosGruesos = eg_dividendo / totalSuperfieConDatosEnHorizonte,
                    MateriaOrganica = mo_dividendo / totalSuperfieConDatosEnHorizonte,
                    IdParcelaInt = -1
                };
                ret.Add(reg);
                hAnt = hEstudio;
                var lSigH = lDatosSueloParcelas.Where(x => x.ProfundidadCM > hAnt);
                if (lSigH.Any())
                    hEstudio = lSigH.Min(x => x.ProfundidadCM);
                else
                    hEstudio = -1;
            }
            if (ret.Count == 0)
                throw new Exception($"No se encontraron datos de suelo para la unidad de cultivo: '{idUC}' en la temporada: '{idTemporada}'");
            return ret;
        }

    }
}
