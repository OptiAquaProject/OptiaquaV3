using Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Siar.Models;

namespace Siar {

    class DatosClimaticos {


        public static List<DatoClimatico> DatosClimaticosSiarList_V2(DateTime desdeFecha, DateTime hastaFecha, int idEstacion) {
            try {
                string sURL;
                var fIni=desdeFecha.ToString("yyyy-MM-dd");
                var fFin = hastaFecha.ToString("yyyy-MM-dd");
                //sURL Ejemplo = https://ias1.larioja.org/apiSiar/servicios/v2/datos-climaticos/503/D?fecha_inicio=2024-06-01&fecha_fin=2024-06-30&paremtro=T
                sURL = $"https://ias1.larioja.org/apiSiar/servicios/v2/datos-climaticos/{idEstacion}/D?fecha_inicio={fIni}&fecha_fin={fFin}";
                sURL += "&parametro=P&parametro=Hr&parametro=ETo&parametro=T&parametro=VV";
                sURL += "&funcion=Med&funcion=Ac";
                WebClient wc = new System.Net.WebClient();
                string json = wc.DownloadString(sURL);
                var dataSiar = Newtonsoft.Json.JsonConvert.DeserializeObject<RootApiSiar_V2>(json);
                List<DatoClimatico> lista = new List<DatoClimatico>();
                if (dataSiar?.success != "true")
                    return lista;

                var lDatos = dataSiar.datos;

                for (var f = desdeFecha; f <= hastaFecha; f = f.AddDays(1)) {
                    var lSub=lDatos.Where(x=>x.fecha==f).ToList();  
                    var dc = new DatoClimatico();
                    dc.IdEstacion= idEstacion;  
                    dc.Fecha= f;
                    dc.TempMedia = ValorParametro(lSub, "T");
                    dc.Eto= ValorParametro(lSub, "ETo");
                    dc.Precipitacion = ValorParametro(lSub, "P");
                    dc.VelViento= ValorParametro(lSub, "VV");
                    dc.HumedadMedia= ValorParametro(lSub, "Hr");
                    if (dc.Precipitacion!=0 || dc.Eto!=0 || dc.HumedadMedia!=0 ||dc.VelViento!=0 || dc.TempMedia!=0)
                        lista.Add(dc);
                }              
                return lista;
            } catch {
                string msgErr = "Error cargando datos climáticos para parametros.\n";
                msgErr += "Desde Fecha: " + desdeFecha.ToShortDateString() + "\n";
                msgErr += "Hasta Fecha: " + hastaFecha.ToShortDateString() + "\n";
                msgErr += "Estación: " + idEstacion.ToString() + "\n";
                throw new Exception(msgErr);
            }
        }

        private static double ValorParametro(List<Dato_V2> lDatos, string paramtro) {
            var valor= lDatos.FirstOrDefault(x=>x.parametro== paramtro )?.valor;
            return valor??0;
        }



        /// <summary>
        /// DatosClimaticosSiarList.
        /// </summary>
        /// <param name="desdeFecha">desdeFecha<see cref="DateTime"/>.</param>
        /// <param name="hastaFecha">hastaFecha<see cref="DateTime"/>.</param>
        /// <param name="idEstacion">idEstacion<see cref="int"/>.</param>
        /// <returns><see cref="List{DatoClimatico}"/>.</returns>
        public static List<DatoClimatico> DatosClimaticosSiarList_V1(DateTime desdeFecha, DateTime hastaFecha, int idEstacion) {
            try {
                string sURL;
                //sURL Ejemplo = "http://apisiar.larioja.org/v1/datos-calculo-riego?estacion=501&fechaInicio=2017-08-07&fechaFin=2017-08-10";
                sURL = "http://apisiar.larioja.org/v1/datos-calculo-riego?";
                sURL += "estacion=" + idEstacion.ToString();
                sURL += "&fechaInicio=" + desdeFecha.ToString("yyyy-MM-dd");
                sURL += "&fechaFin=" + hastaFecha.ToString("yyyy-MM-dd");
                WebClient wc = new System.Net.WebClient();
                string json = wc.DownloadString(sURL);
                RootApiSiar_V1 dataSiar = Newtonsoft.Json.JsonConvert.DeserializeObject<RootApiSiar_V1>(json);
                List<DatoClimatico> lista = new List<DatoClimatico>();
                if (dataSiar?.data == null)
                    return lista;
                foreach (DatoClimaticoApiSiar_V1 dat in dataSiar.data) {
                    DatoClimatico dc = new DatoClimatico {
                        IdEstacion = int.Parse(dat.Estacion),
                        Fecha = Convert.ToDateTime(dat.Fecha),
                        Eto = dat.ET0 == "NA" ? 0 : double.Parse(dat.ET0, CultureInfo.InvariantCulture),
                        TempMedia = dat.TAirMd == "NA" ? 0 : double.Parse(dat.TAirMd, CultureInfo.InvariantCulture),
                        HumedadMedia = dat.HRMn == "NA" ? 0 : double.Parse(dat.HRMn, CultureInfo.InvariantCulture),
                        VelViento = dat.VWindMd == "NA" ? 0 : double.Parse(dat.VWindMd, CultureInfo.InvariantCulture),
                        Precipitacion = dat.PAcum == "NA" ? 0 : double.Parse(dat.PAcum, CultureInfo.InvariantCulture)
                    };
                    lista.Add(dc);
                }
                return lista;
            } catch {
                string msgErr = "Error cargando datos climáticos para parametros.\n";
                msgErr += "Desde Fecha: " + desdeFecha.ToShortDateString() + "\n";
                msgErr += "Hasta Fecha: " + hastaFecha.ToShortDateString() + "\n";
                msgErr += "Estación: " + idEstacion.ToString() + "\n";
                throw new Exception(msgErr);
            }
        }

    }
}
