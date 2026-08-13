using DatosOptiaqua;
using NetTopologySuite.IO;
using NPoco;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoPackageReaderFW {
    internal class ImportMapas {


        public static Database DBMapa(string sqliteFileName) {
            // System.Data.SQLite no tiene versión para .NET moderno: se usa Microsoft.Data.Sqlite,
            // que es el proveedor oficial. La cadena ya no lleva "Version=3", que ese proveedor
            // no reconoce.
            string cnnStr = $"Data Source={sqliteFileName}";
            return new Database(cnnStr, DatabaseType.SQLite, Microsoft.Data.Sqlite.SqliteFactory.Instance);

        }


        public static string ImportarMapaSuelo(string fileNameGpkg, string idVersion,int nivel) {
            try {
                var dbOptiaqua = DB.ConexionOptiaqua;
                var dbMapa = DBMapa(fileNameGpkg);
                var srsid = dbMapa.Single<int?>("SELECT srs_id FROM gpkg_contents");
                if (srsid == null || srsid != 4326) {
                    return "El mapa no parece estar en la proyección 4326";
                }
                var lMapa = dbMapa.Fetch<MapaSueloPocoSqlLite>(@" SELECT * FROM [MapaSuelo-Optiaqua]");
                var reader = new GeoPackageGeoReader();
                var lIns = new List<MapaSueloPoco>();                
                foreach (var mapa in lMapa) {
                    var geom2 = reader.Read(mapa.Geom);
                    var poligono = geom2.ToString();

                    var nMapa = new MapaSueloPoco{                        
                        ID = mapa.ID,
                        IdVersion = idVersion,
                        Nivel=nivel,                        
                        Geom = poligono,
                        REF_CATAST = mapa.REF_CATAST,
                        HS_ARENA_Porc = mapa.HS_ARENA_Porc,
                        HS_EGRUESO_Porc = mapa.HS_EGRUESO_Porc,
                        HS_ARCILLA_Porc = mapa.HS_ARCILLA_Porc,
                        HS_LIMO_Porc = mapa.HS_LIMO_Porc,
                        HS_MATORG_Porc = mapa.HS_MATORG_Porc,
                        HS_ESPESOR_cm = mapa.HS_ESPESOR_cm,
                        HS_TEXTURA = mapa.HS_TEXTURA,
                        PROF_EFECTIVA_cm = mapa.PROF_EFECTIVA_cm,
                        
                        SC_ESPESOR_cm =mapa.SC_ESPESOR_cm,
                        SC_ARENA_Porc = mapa.SC_ARENA_Porc,
                        SC_ARCILLA_Porc = mapa.SC_ARCILLA_Porc,
                        SC_LIMO_Porc = mapa.SC_LIMO_Porc,
                        SC_EGRUESO_Porc = mapa.SC_EGRUESO_Porc,
                        SC_MATORG_Porc=mapa.SC_MATORG_Porc,
                        OBSERVACIONES=mapa.OBSERVACIONES
                    };
                    lIns.Add(nMapa);
                }
                dbOptiaqua.InsertBatch(lIns);
                var sql = "update MapaSuelo SET Geom.STSrid = 4326; ";
                dbOptiaqua.Execute(sql);
                return "";
            } catch (Exception ex) {
                return ex.Message;
            }
        }

    }
}
