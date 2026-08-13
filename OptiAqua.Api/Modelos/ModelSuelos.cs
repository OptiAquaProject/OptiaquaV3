using NPoco;








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
