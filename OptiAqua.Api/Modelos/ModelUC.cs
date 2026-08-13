using NPoco;
namespace WebApi {

    public class ImportItemUCExcel {
        public int Linea { set; get; }
        /// <summary>
        /// Gets or sets the IdUnidadCultivo.
        /// </summary>
        /// 

        public string IdUnidadCultivo { set; get; }

        /// <summary>
        /// Gets or sets the IdRegante.
        /// </summary>
        public int IdGadminRegante { set; get; }

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

        /// <summary>
        /// Identificación de la parcela por Municipio, Poligono, Parcela
        /// </summary>
        public int Provincia { set; get; }
        public int Municipio { set; get; }
        public int Poligono { set; get; }
        public int Parcela { set; get; }        
    }
}