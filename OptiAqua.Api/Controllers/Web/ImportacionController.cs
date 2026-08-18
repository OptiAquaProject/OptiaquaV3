namespace WebApi {
    using DatosOptiaqua;
    using GeoPackageReaderFW;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using webapi.Utiles;
    using static DatosOptiaqua.ImportacionUC;

    /// <summary>
    /// Importación masiva de unidades de cultivo, análisis de suelo y mapas.
    /// Requiere sesión de administrador (cookie). Antes pedía usuario y contraseña en cada
    /// formulario —el parche que originó esta técnica—; ahora basta estar identificado como admin.
    /// </summary>
    [Authorize(AuthenticationSchemes = "Cookies", Roles = "admin")]
    public class ImportacionController : Controller {

        private string Usuario() => User.Nif() ?? "admin";

        public IActionResult Importacion() {
            ViewBag.Title = "Importación para la creación masiva de unidades de cultivo.";
            return View();
        }


        public IActionResult EspecificacionesImportacion() {
            return View();
        }


        public IActionResult ImportacionMapas() {
            var lMapaVersion = DB.DatosMapasVersion();
            ViewBag.lMapaVersion = lMapaVersion;
            return View();
        }

        [HttpPost]
        public string ImportarMapasPost() {
            string fileNameBase = null;
            try {
                if (Request.Form.Files.Count == 0)
                    return "No se ha recibido ningún fichero";
                IFormFile fichero = Request.Form.Files[0];

                // El fichero se guardaba con la extensión que enviase el cliente, sin comprobarla.
                var extension = Path.GetExtension(fichero.FileName);
                if (!string.Equals(extension, ".gpkg", StringComparison.OrdinalIgnoreCase))
                    return "Sólo se admiten ficheros con extensión .gpkg";

                fileNameBase = Path.Combine(DB.PathRoot, DateTime.Now.Ticks.ToString() + ".gpkg");
                using (var destino = System.IO.File.Create(fileNameBase)) {
                    fichero.CopyTo(destino);
                }

                var idVersion = Request.Form["idVersion"].ToString();
                int nivel;
                if (!int.TryParse(Request.Form["nivel"].ToString(), out nivel))
                    return "El nivel indicado no es válido";

                DB.EliminarMapas(idVersion, nivel);
                var err = ImportMapas.ImportarMapaSuelo(fileNameBase, idVersion, nivel);
                if (!string.IsNullOrWhiteSpace(err)) {
                    Log.Aviso("La importación del mapa versión " + idVersion + " nivel " + nivel + " terminó con error: " + err, null);
                    return err;
                }
                CacheDatosHidricos.RecalculaSuelos();
                Log.Info("Importado mapa versión " + idVersion + " nivel " + nivel + " por " + Usuario());
                return "OK";
            } catch (Exception ex) {
                Log.Error("Importación de mapas", ex);
                return "Error en la importación. Consulte el registro del servidor.";
            } finally {
                try {
                    if (fileNameBase != null && System.IO.File.Exists(fileNameBase))
                        System.IO.File.Delete(fileNameBase);
                } catch (Exception exBorrado) {
                    Log.Aviso("No se pudo borrar el fichero temporal " + fileNameBase, exBorrado);
                }
            }
        }

        [HttpPost]
        public string EliminarMapas() {
            try {
                var idVersion = Request.Form["paramJson[idVersion]"].ToString();
                int nivel;
                if (!int.TryParse(Request.Form["paramJson[nivel]"].ToString(), out nivel))
                    return "El nivel indicado no es válido";

                DB.EliminarMapas(idVersion, nivel);
                Log.Info("Eliminado mapa versión " + idVersion + " nivel " + nivel + " por " + Usuario());
                return "OK";
            } catch (Exception ex) {
                Log.Error("Eliminación de mapas", ex);
                return "Error al eliminar el mapa. Consulte el registro del servidor.";
            }
        }

        [HttpPost]
        public string ImportarUCPost() {
            try {
                if (Request.Form.Files.Count == 0)
                    return "Error";
                IFormFile fichero = Request.Form.Files[0];

                List<ImportItemUCExcel> excel;
                using (var flujo = fichero.OpenReadStream()) {
                    excel = MiniExcelLibs.MiniExcel.Query<ImportItemUCExcel>(flujo).ToList();
                }

                // El propietario de cada UC se resuelve por el código del Excel (IdGadminRegante),
                // no por quien importa; el nif/pass eran vestigiales.
                int nImportados;
                var lErrores = ImportarUcFromExcel(Usuario(), "", excel, out nImportados);
                if (lErrores.Count > 0)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(lErrores);
                CacheDatosHidricos.RecalculaSuelos();
                Log.Info("Importadas " + nImportados + " unidades de cultivo por " + Usuario());
                return "OK:" + nImportados.ToString();
            } catch (Exception ex) {
                Log.Error("Importación de unidades de cultivo desde Excel", ex);
                return "Error: no se pudo completar la importación. Consulte el registro del servidor.";
            }
        }
    }
}
