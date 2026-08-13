namespace DatosOptiaqua {
    using NPoco;
    using System.Collections.Generic;

    public static partial class DB {

        internal static List<ItemMapaVersion> DatosMapasVersion() {
            Database db = ConexionOptiaqua;            
            var lMapa=db.Fetch<ItemMapaVersion>("SELECT IdVersion, Nivel, COUNT(ID) AS NumRegistros FROM dbo.MapaSuelo GROUP BY IdVersion, Nivel");
            return lMapa;
        }

        public static void EliminarMapas(string idVersion,int nivel) {
            Database db = ConexionOptiaqua;
            db.Execute($"delete from MapaSuelo where IdVersion='{idVersion}' and Nivel={nivel}");            
        }

    }
}

    