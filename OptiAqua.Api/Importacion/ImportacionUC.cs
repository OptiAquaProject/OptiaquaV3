namespace DatosOptiaqua {
    using Models;
    using NPoco;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using webapi.Utiles;
    using WebApi;
    
    /// <summary>
    /// Capa de acceso a las base de datos OptiAqua y Nebula en SQl Server
    /// Para simplificar el acceso se hace uso de la librería NPoco - https://github.com/schotime/NPoco
    /// La cadena de conexión CadenaConexionOptiAqua se define como parámetro de la aplicación.
    /// La cadena de conexión Nebula se define como parámetro de la aplicación.
    /// </summary>
    public static class ImportacionUC {
        /// <summary>
        /// Defines the <see cref="ImportItemUC" />.
        /// </summary>
        public class ImportItemUC {
            public int Linea { set; get; }
            /// <summary>
            /// Gets or sets the IdUnidadCultivo.
            /// </summary>
            /// 

            public string IdUnidadCultivo { set; get; }

            /// <summary>
            /// Gets or sets the IdRegante.
            /// </summary>
            public int IdRegamte { set; get; }

            /// <summary>
            /// Gets or sets the Alias.
            /// </summary>
            public string Paraje { set; get; }

            /// <summary>
            /// Gets or sets the IdTemporada.
            /// </summary>
            public string IdTemporada { set; get; }

            /// <summary>
            /// Gets or sets the IdCultivo.
            /// </summary>
            public int IdCultivo { set; get; }

            /// <summary>
            /// Gets or sets the FechaSiembra.
            /// </summary>
            public DateTime FechaSiembra { set; get; }

            /// <summary>
            /// Gets or sets the IdTipoRiego.
            /// </summary>
            public int IdTipoRiego { set; get; }

            /// <summary>
            /// Gets or sets the SuperficieM2.
            /// </summary>
            public double? SuperficieM2 { set; get; }

            public List<int> IdParcelaIntList { set; get; }

            public int IdEstacion { set; get; }
        }

        /// <summary>
        /// Defines the <see cref="ImportItemAnalisisSuelo" />.
        /// </summary>
        public class ImportItemAnalisisSuelo {

            public int NLinea { set; get; }
            /// <summary>
            /// Gets or sets the Temporada.
            /// </summary>
            public string Temporada { set; get; }

            /// <summary>
            /// Gets or sets the Provincia.
            /// </summary>
            public int Provincia { set; get; }

            /// <summary>
            /// Gets or sets the Municipio.
            /// </summary>
            public int Municipio { set; get; }

            /// <summary>
            /// Gets or sets the Poligono.
            /// </summary>
            public int Poligono { set; get; }

            /// <summary>
            /// Gets or sets the IdParcelaIntList.
            /// </summary>
            public List<int> IdParcelaIntList { set; get; }

            /// <summary>
            /// Gets or sets the ElementoGruesos.
            /// </summary>
            public double ElementoGruesos { set; get; }

            /// <summary>
            /// Gets or sets the MateriaOrganica.
            /// </summary>
            public double MateriaOrganica { set; get; }

            /// <summary>
            /// Gets or sets the Arcilla.
            /// </summary>
            public double Arcilla { set; get; }

            /// <summary>
            /// Gets or sets the Arena.
            /// </summary>
            public double Arena { set; get; }

            /// <summary>
            /// Gets or sets the Limo.
            /// </summary>
            public double Limo { set; get; }
        }

        /// <summary>
        /// Defines the <see cref="ErrorItem" />.
        /// </summary>
        public class ErrorItem {
            /// <summary>
            /// Gets or sets the NLinea.
            /// </summary>
            public int NLinea { set; get; }

            /// <summary>
            /// Gets or sets the Descripcion.
            /// </summary>
            public string Descripcion { set; get; }
        }

        class itemClienteIds {
            public int ID_FARMER { set; get; }
            public int ID_GADMIN { set; get; }
        }

        internal static List<ErrorItem> ImportarUcFromExcel(string nif, string pass, List<ImportItemUCExcel> lineas,out int nImportados) {
            
            int nLinea = 0;
            nImportados = 0;
            List<ErrorItem> lErrores = new List<ErrorItem>();            
            var lItemUC = new List<ImportItemUC>();
            var lCultivosActivos=DB.ConexionOptiaqua.Fetch<int>("select IdCultivo from Cultivo where Activo=1");
            var lGrp=lineas.GroupBy(x=>x.IdUnidadCultivo).Select(x=>new {x.Key,list=x.ToList()});            

            foreach (var grp in lGrp) {
                try {
                    var linea = grp.list.First();
                    nLinea=linea.Linea;
                    if (nLinea == 0)
                        continue;     
                    
                    if (string.IsNullOrEmpty(linea.IdUnidadCultivo)) {
                        lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = "IdUnidadCultivo ha de tener un valor" });
                        continue;
                    }

                    if (grp.list.Select(x => x.IdGadminRegante).Distinct().Count() > 1) {
                        lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = $"La UC con Id:{linea.IdUnidadCultivo} está asociada a más de un regante" });
                        continue;
                    }

                    if (grp.list.Select(x => x.IdTipoRiego).Distinct().Count() > 1) {
                        lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = $"La UC con Id:{linea.IdUnidadCultivo} está asociada a más de un tipo de riego" });
                        continue;
                    }

                    if (grp.list.Select(x => x.FechaSiembra).Distinct().Count() > 1) {
                        lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = $"La UC con Id:{linea.IdUnidadCultivo} tiene más de una fecha de siembra" });
                        continue;
                    }

                    if (grp.list.Select(x => x.IdTemporada).Distinct().Count() > 1) {
                        lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = $"La UC con Id:{linea.IdUnidadCultivo} tiene más de una temporada" });
                        continue;
                    }


                    if (grp.list.Select(x => x.IdCultivo).Distinct().Count() > 1) {
                        lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = $"La UC con Id:{linea.IdUnidadCultivo} tiene más de un tipo de cultivo" });
                        continue;
                    }

                    var idRegante = DB.ConexionOptiaqua.SingleOrDefault<int?>($"select IdRegante from Regante where IdGadmin={linea.IdGadminRegante}");


                    if (idRegante == null) {
                        lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = $"No encontró el Regante con código IdGadmin '{linea.IdGadminRegante}' " });
                        continue;
                    }

                    if (!lCultivosActivos.Contains(linea.IdCultivo)) {
                        lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = $"El IdCultivo '{linea.IdCultivo}' no existe en la tabla de cultivos o no está activo." });
                        continue;
                    }                    
                    
                    if (!DB.TemporadaExists(linea.IdTemporada)) {
                        lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = $"La temporada '{linea.IdTemporada}' no existe" });
                        continue;
                    }


                    var lParcelasId = new List<int>();                    
                    foreach (var l in grp.list ) {
                        var idParcela = DB.ParcelaIFromPMPP(l.Provincia, l.Municipio, l.Poligono, l.Parcela);
                        if (idParcela == null) {
                            lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = $"No se encuentra parcela ({l.Provincia},{l.Municipio},{l.Poligono},{l.Parcela})" });
                            goto continue_for;
                        }
                        lParcelasId.Add(idParcela.Value);
                    }
                    if (lParcelasId.Count == 0) {
                        lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = "No se han indicado parcelas válidas" });
                        continue;
                    }

                    foreach (var idPar in lParcelasId) {
                        if (!DB.ParcelaExits(idPar)) {
                            lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = $"No existe la parcela '{idPar}'" });
                            goto continue_for;
                        }
                        if (linea.SuperficieM2== 0) {
                            var sup = DB.ParcelaSuperficieM2(idPar);
                            if (sup == null) {
                                lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = $"No se ha indicado superficie para la parcela '{idPar} y tampoco tiene valor en la BD'" });
                                goto continue_for;
                            }
                        }
                    }

                    int idEstacion = DB.EstacionDeParcela(lParcelasId[0]);

                    ImportItemUC item = new ImportItemUC {
                        SuperficieM2=grp.list.Sum(x=>x.SuperficieM2),
                        IdParcelaIntList= lParcelasId,
                        IdTemporada=linea.IdTemporada,
                        IdCultivo=linea.IdCultivo,
                        IdRegamte=idRegante.Value,
                        IdUnidadCultivo= linea.IdUnidadCultivo,
                        Linea = linea.Linea,
                        Paraje = linea.Paraje,
                        FechaSiembra= linea.FechaSiembra,
                        IdEstacion= idEstacion,
                        IdTipoRiego = linea.IdTipoRiego
                    };
                    //Importar(item, param.IdTemporada, param.IdTemporadaAnterior);
                    lItemUC.Add(item);
                continue_for:
                    { }// noop
                } catch (Exception ex) {
                    lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = ex.Message });
                }
            }
            if (lErrores.Count > 0)
                return lErrores;
            var lErr=ImportarUC(lItemUC,out nImportados);            
            return lErr;
        }

        private static List<ErrorItem> ImportarUC(List<ImportItemUC> lItemUC,out int nImportados ) {
            var ret=new List<ErrorItem>();  
            Database db = DB.ConexionOptiaqua;
            db.BeginTransaction();
            int nLin = 0;
            nImportados = 0;
            try {
                foreach (var item in lItemUC) {
                    nLin = item.Linea;
                    var idTemporada = item.IdTemporada;
                    UnidadCultivo uc = new Models.UnidadCultivo {
                        IdUnidadCultivo = item.IdUnidadCultivo,
                        Alias = item.Paraje.Truncate(30),
                        IdRegante = item.IdRegamte,
                        TipoSueloDescripcion = "",
                        TomarRiegosDeIdCultivo = ""
                    };
                    db.Save(uc);
                    UnidadCultivoParcelaSave(db, item.IdUnidadCultivo, idTemporada, item.IdRegamte, item.IdParcelaIntList);

                    //Crear la Etapas de crecimiento según el cultivo indicado y al fecha de siembra
                    UnidadCultivoCultivoTemporadaSave(db, item.IdUnidadCultivo, idTemporada, item.IdCultivo, item.IdRegamte, item.IdTipoRiego, item.FechaSiembra);

                    // Sólo si se indicar un valor la superficie se almacena valor. En caso contrario se calculará porla parcelas indicadas
                    if (item.SuperficieM2 != null && item.SuperficieM2 != 0)
                        UnidadCultivoSuperficieSave(db, item.IdUnidadCultivo, idTemporada, (double)item.SuperficieM2);

                }
                db.CompleteTransaction();
                nImportados = lItemUC.Count;
                return ret;
            } catch (Exception ex) {
                db.AbortTransaction();
                ret.Add(new ErrorItem { NLinea = nLin, Descripcion = ex.Message });
                return ret;
            }
        }

        /// <summary>
        /// The UnidadCultivoParcelaSave.
        /// </summary>
        /// <param name="db">The db<see cref="Database"/>.</param>
        /// <param name="idUnidadCultivo">The idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">The idTemporada<see cref="string"/>.</param>
        /// <param name="idRegante">The idRegante<see cref="int"/>.</param>
        /// <param name="lIdParcelaInt">The lIdParcelaInt<see cref="List{int}"/>.</param>
        private static void UnidadCultivoParcelaSave(Database db, string idUnidadCultivo, string idTemporada, int idRegante, List<int> lIdParcelaInt) {
            UnidadCultivoParcela ucp = new UnidadCultivoParcela {
                IdUnidadCultivo = idUnidadCultivo,
                IdTemporada = idTemporada,
                IdParcelaInt = 0,
                IdRegante = idRegante
            };
            lIdParcelaInt.ForEach(x => {
                ucp.IdParcelaInt = x;
                db.Save(ucp);
            });
        }

        /// <summary>
        /// The UnidadCultivoSuperficieSave.
        /// </summary>
        /// <param name="db">The db<see cref="Database"/>.</param>
        /// <param name="idUnidadCultivo">The idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">The idTemporada<see cref="string"/>.</param>
        /// <param name="superficieM2">The superficieM2<see cref="double"/>.</param>
        private static void UnidadCultivoSuperficieSave(Database db, string idUnidadCultivo, string idTemporada, double superficieM2) {
            /*
            UnidadCultivoSuperficie r = new UnidadCultivoSuperficie {
                IdTemporada = idTemporada,
                IdUnidadCultivo = idUnidadCultivo,
                SuperficieM2 = superficieM2
            };
            db.Save(r);
            */
            UnidadCultivoCultivo ucc = db.SingleOrDefault<UnidadCultivoCultivo>("WHERE IdUnidadCultivo=@0 AND IdTemporada=@1", idUnidadCultivo, idTemporada);
            if (ucc != null) {
                ucc.SuperficieM2 = superficieM2;
                db.Save(ucc);
            }
        }

        /// <summary>
        /// The UnidadCultivoCultivoTemporadaSave.
        /// </summary>
        /// <param name="db">The db<see cref="Database"/>.</param>
        /// <param name="IdUnidadCultivo">The IdUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">The idTemporada<see cref="string"/>.</param>
        /// <param name="idCultivo">The idCultivo<see cref="int"/>.</param>
        /// <param name="idRegante">The idRegante<see cref="int"/>.</param>
        /// <param name="idTipoRiego">The idTipoRiego<see cref="int"/>.</param>
        /// <param name="fechaSiembra">The fechaSiembra<see cref="string"/>.</param>
        private static void UnidadCultivoCultivoTemporadaSave(Database db, string IdUnidadCultivo, string idTemporada, int idCultivo, int idRegante, int idTipoRiego, DateTime fechaSiembra) {
            try {

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
                    Pluviometria = DB.PluviometriaTipica(idTipoRiego)
                };
                db.Insert(uniCulCul);

                // Leer Cultivo Etapas de IdCultivo
                List<CultivoEtapas> listaCF = db.Fetch<CultivoEtapas>("Select * from CultivoEtapas Where IdCultivo=@0", idCultivo);
                if (listaCF.Count == 0) {
                    throw new Exception("Error. No existe una definición de las Etapas para el cultivo indicado.");
                }

                DateTime fechaEtapa = fechaSiembra;
                foreach (CultivoEtapas cf in listaCF) {
                    UnidadCultivoCultivoEtapas pcf = new UnidadCultivoCultivoEtapas {
                        IdUnidadCultivo = uniCulCul.IdUnidadCultivo,
                        IdTemporada = uniCulCul.IdTemporada,
                        IdEtapaCultivo = cf.OrdenEtapa,
                        IdTipoEstres = cf.IdTipoEstres,
                        DuracionDiasEtapa = cf.DuracionDiasEtapa,
                        Etapa = cf.Etapa,
                        FechaInicioEtapa = fechaEtapa,
                        IdTipoCalculoAltura = cf.IdTipoCalculoAltura,
                        IdTipoCalculoCobertura = cf.IdTipoCalculoCobertura,
                        IdTipoCalculoLongitudRaiz = cf.IdTipoCalculoLongitudRaiz,
                        SeAplicaRiego = cf.SeAplicaRiego,
                        ParametrosJson = cf.ParametrosJson,
                        CobInicial = cf.CobInicial,
                        CobFinal = cf.CobFinal,
                        DefinicionPorDias = cf.DefinicionPorDias,
                        FactorDeAgotamiento = cf.FactorAgotamiento,
                        KcFinal = cf.KcFinal,
                        KcInicial = cf.KcInicial,
                        FechaInicioEtapaConfirmada = null,
                        AlturaFinal = cf.AlturaFinal,
                        AlturaInicial = cf.AlturaInicial
                    };
                    fechaEtapa = fechaEtapa.AddDays(cf.DuracionDiasEtapa);
                    db.Insert(pcf);
                }
                return;
            } catch (Exception ex) {
                throw new Exception("Error. No existe una definición de las Etapas para el cultivo indicado." + ex.Message);
            }
        }

        /*
        /// <summary>
        /// Importación del fichero csv con los datos de los análisis de suelos.
        /// </summary>
        /// <param name="param">.</param>
        /// <returns>.</returns>
        internal static List<ErrorItem> ImportarAnalisisSuelos(ImportaAnalisisCultivoPost param) {
            string csv = param.CSV;
            int nLinea = 1;
            List<ErrorItem> lErrores = new List<ErrorItem>();
            string[] lineas = csv.Split('\n');
            foreach (string linea in lineas) {
                try {
                    if (linea == lineas.First())
                        continue;
                    if (linea.Length < 10)
                        continue;
                    string[] lItemsLinea = linea.Replace('\t', ';').Split(';');
                    ImportItemAnalisisSuelo item = new ImportItemAnalisisSuelo {
                        NLinea = nLinea,
                        Temporada = lItemsLinea[0],
                        Provincia = int.Parse(lItemsLinea[1]),
                        Municipio = int.Parse(lItemsLinea[2]),
                        Poligono = int.Parse(lItemsLinea[3]),
                        IdParcelaIntList = lItemsLinea[4].Replace("-", ",").Split(',').Select(int.Parse).ToList(),
                        ElementoGruesos = double.Parse(lItemsLinea[5]),
                        MateriaOrganica = double.Parse(lItemsLinea[6]),
                        Arcilla = double.Parse(lItemsLinea[7]),
                        Arena = double.Parse(lItemsLinea[8]),
                        Limo = double.Parse(lItemsLinea[9])
                    };
                    ErrorItem errLin = null;ImportarAnalisisSuelo(item);
                    lErrores.AddRange(errLin);
                } catch (Exception ex) {
                    lErrores.Add(new ErrorItem { NLinea = nLinea, Descripcion = ex.Message });
                }
                nLinea++;
            }
            return lErrores;
        }
        */

        /*
                private static List<ErrorItem> ImportarAnalisisSuelo(ImportItemAnalisisSuelo item) {
                    List<ErrorItem> ret = new List<ErrorItem>();
                    Database db = DB.ConexionOptiaqua;
                    db.BeginTransaction();
                    try {
                        foreach (int parcela in item.IdParcelaIntList) {
                            string sql = @"";
                            sql += $" SELECT dbo.UnidadCultivoParcela.IdUnidadCultivo ";
                            sql += $" FROM dbo.Parcela INNER JOIN ";
                            sql += $" dbo.UnidadCultivoParcela ON dbo.Parcela.IdParcelaInt = dbo.UnidadCultivoParcela.IdParcelaInt ";
                            sql += $" WHERE";
                            sql += $" (dbo.UnidadCultivoParcela.IdTemporada = '{item.Temporada}') AND ";
                            sql += $" (dbo.Parcela.IdProvincia = {item.Provincia}) AND ";
                            sql += $" (dbo.Parcela.IdMunicipio = {item.Municipio}) AND";
                            sql += $" (dbo.Parcela.IdPoligono = {item.Poligono}) AND";
                            sql += $" (dbo.Parcela.IdParcela = '{parcela}')";

                            string idUC = db.SingleOrDefault<string>(sql);
                            if (idUC == null) {
                                ret.Add(new ErrorItem { NLinea = item.NLinea, Descripcion = "No se encuentra parcela: "+parcela.ToString()});
                                continue;
                            }
                            var  ucs = new UnidadCultivoSuelo {
                                IdUnidadCultivo = idUC,
                                IdTemporada = item.Temporada,
                                Arcilla = item.Arcilla,
                                Arena = item.Arena,
                                ElementosGruesos = item.ElementoGruesos,
                                Limo = item.Limo,
                                MateriaOrganica = item.MateriaOrganica,
                                IdHorizonte = 1,
                                ProfundidadHorizonte = 1
                            };
                            db.Save(ucs);
                        }
                        db.CompleteTransaction();
                    } catch (Exception) {
                        db.AbortTransaction();

                    }
                    return ret;
                }
        */
    }
}
