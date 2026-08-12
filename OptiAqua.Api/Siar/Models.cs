using NPoco;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Policy;

/// <summary>
/// Modelo de datos de la aplicación
/// </summary>
namespace Siar.Models {

    public class RootApiSiar_V1 {
        public List<DatoClimaticoApiSiar_V1> data { get; set; }
    }

    public class DatoClimaticoApiSiar_V1 {
        public DateTime Fecha { get; set; }
        public string Estacion { get; set; }
        public string TAirMd { get; set; }
        public string HRMn { get; set; }
        public string PAcum { get; set; }
        public string ET0 { get; set; }
        public string VWindMd { get; set; }
    }


    public class RootApiSiar_V2 {
        public string count { get; set; }
        public string success { get; set; }
        public List<Dato_V2> datos { get; set; }
    }

    public class Dato_V2 {
        public string dato_valido { get; set; }
        public DateTime fecha { get; set; }
        public DateTime fecha_modificacion { get; set; }
        public string funcion_agregacion { get; set; }
        public string nivel_validacion { get; set; }
        public string parametro { get; set; }
        public string posicion { get; set; }
        public string validado_visualmente { get; set; }
        public double valor { get; set; }
    }

}

