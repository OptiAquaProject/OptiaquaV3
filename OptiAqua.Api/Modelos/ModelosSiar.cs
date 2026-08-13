using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Policy;

/// <summary>
/// Modelo de datos de la aplicación
/// </summary>
namespace Siar.Models {






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

