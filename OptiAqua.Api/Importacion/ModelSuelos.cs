using System;
using NPoco;


[TableName("MapaCatastral")]
[PrimaryKey("ID", AutoIncrement = false)]
public class MapaCatastralSqlitePoco {
    public int fid { get; set; }
    public byte[] GEOM { get; set; }
    public string ID_MUESTRA { get; set; }
    public string ORIGEN_MUESTRA { get; set; }
    public DateTime? FECHA_MUESTRA { get; set; }
    public int? PROVINCIA { get; set; }
    public int? MUNICIPIO { get; set; }
    public string POLIGONO { get; set; }
    public string PARCELA { get; set; }
    public string REF_CATAST { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public double? HS_PROF_cm { get; set; }
    public double? HS_ARENA_Porc { get; set; }
    public double? HS_ARCILLA_Porc { get; set; }
    public double? HS_LIMO_Porc { get; set; }
    public string HS_TEXTURA { get; set; }
    public double? HS_EGRUESO_Porc { get; set; }
    public double? HS_MATORG_Porc { get; set; }
    public double? HS_PH { get; set; }
    public double? PROF_EFECTIVA_cm { get; set; }
    public string OBSERVACIONES { get; set; }
}


[TableName("MapaBase")]
[PrimaryKey("IdVersion,ID", AutoIncrement = false)]
public class MapaBaseSqlserverPoco {
    public string IdVersion { get; set; }
    public int ID { get; set; }
    public int? ID_INTER_PROSP_RELIEVE { get; set; }
    public string Geom { get; set; }
    public string Litologia { get; set; }
    public string Cuenca { get; set; }
    public string Proy_GEODE { get; set; }
    public long? COORD_X { get; set; }
    public long? COORD_Y { get; set; }
    public string CLASIFICAC { get; set; }
    public double? HS_PROF_cm { get; set; }
    public double? HS_ARENA_Porc { get; set; }
    public double? HS_ARCILLA_Porc { get; set; }
    public double? HS_LIMO_Porc { get; set; }
    public string HS_TEXTURA { get; set; }
    public double? HS_EGRUESO_Porc { get; set; }
    public double? HS_CALIZAA_g_Kg { get; set; }
    public double? HS_CARBONA_g_Kg { get; set; }
    public double? HS_CE_dS_m { get; set; }
    public double? HS_MATORG_Porc { get; set; }
    public double? HS_PH { get; set; }
    public double? HS_CIC_meq100g { get; set; }
    public double? SC_ESPESOR_cm { get; set; }
    public double? SC_ARENA_Porc { get; set; }
    public double? SC_ARCILLA_Porc { get; set; }
    public double? SC_LIMO_Porc { get; set; }
    public string SC_TEXTURA { get; set; }
    public double? SC_EGRUESO_Porc { get; set; }
    public double? SC_CALIZAA_g_Kg { get; set; }
    public double? SC_CARBONA_g_Kg { get; set; }
    public double? SC_CE_dS_m { get; set; }
    public double? SC_MATORG_Porc { get; set; }
    public double? SC_PH { get; set; }
    public double? SC_CIC_meq_100g { get; set; }
    public double? PROF_EFECTIVA_cm { get; set; }
    public string GEOFORMA { get; set; }
    public string SUELO1 { get; set; }
    public string SUELO2 { get; set; }
    public string SUELO3 { get; set; }
}


public class ItemMapaVersion {
    public string IdVersion { get; set; }
    public int Nivel { get; set; }
    public int NumRegistros { get; set; }
}

[TableName("MapaSuelo")]
[PrimaryKey("IdMapaSuelo", AutoIncrement = true)]
public class MapaSueloPoco {
    public int IdMapaSuelo { get; set; }
    public string IdVersion { get; set; }
    public int Nivel { get; set; }
    
    public int ID { get; set; }
    public string Geom { get; set; }
    public string REF_CATAST { get; set; }

    // HS Horizonte superficial
    public double? HS_ESPESOR_cm { get; set; }
    public double? HS_ARENA_Porc { get; set; }
    public double? HS_ARCILLA_Porc { get; set; }
    public double? HS_LIMO_Porc { get; set; }
    public string HS_TEXTURA { get; set; }
    public double? HS_EGRUESO_Porc { get; set; }
    public double? HS_MATORG_Porc { get; set; }
    public double? PROF_EFECTIVA_cm { get; set; }

    // SC zona de Control
    public double? SC_ESPESOR_cm { get; set; }
    public double? SC_ARENA_Porc { get; set; }
    public double? SC_ARCILLA_Porc { get; set; }
    public double? SC_LIMO_Porc { get; set; }    
    public double? SC_EGRUESO_Porc { get; set; }
    public double? SC_MATORG_Porc { get; set; }

    public string OBSERVACIONES { get; set; }
}


[TableName("MapaSuelo")]
[PrimaryKey("Id", AutoIncrement = false)]
public class MapaSueloPocoSqlLite {
    public int IdMapaSuelo { get; set; }
    public string IdVersion { get; set; }
    public int Nivel { get; set; }

    public int ID { get; set; }
    public byte[] Geom { get; set; }
    public string REF_CATAST { get; set; }

    public double? HS_ESPESOR_cm { get; set; }
    public double? HS_ARENA_Porc { get; set; }
    public double? HS_ARCILLA_Porc { get; set; }
    public double? HS_LIMO_Porc { get; set; }
    public string HS_TEXTURA { get; set; }
    public double? HS_EGRUESO_Porc { get; set; }
    public double? HS_MATORG_Porc { get; set; }

    public double? PROF_EFECTIVA_cm { get; set; }

    public double? SC_ESPESOR_cm { get; set; }
    public double? SC_ARENA_Porc { get; set; }
    public double? SC_ARCILLA_Porc { get; set; }
    public double? SC_LIMO_Porc { get; set; }
    public double? SC_EGRUESO_Porc { get; set; }
    public double? SC_MATORG_Porc { get; set; }

    public string OBSERVACIONES { get; set; }
}
