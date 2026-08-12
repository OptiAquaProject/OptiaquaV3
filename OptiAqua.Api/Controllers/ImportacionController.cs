namespace WebApi {
    using DatosOptiaqua;
    using GeoPackageReaderFW;
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
    /// </summary>
    public class ImportacionController : Controller {

        /// <summary>
        /// Comprueba las credenciales recibidas por formulario y que correspondan a un administrador.
        ///
        /// Estas acciones no pueden apoyarse en [Authorize] porque las invoca un formulario de
        /// navegador, no un cliente con token JWT. Se sigue el mismo esquema que ImportarUCPost,
        /// que ya pedía usuario y contraseña.
        /// </summary>
        private static bool CredencialesAdminValidas(string nif, string pass, out string error) {
            error = null;
            Models.Regante regante;
            if (!DB.IsCorrectPassword(new Models.LoginRequest { NifRegante = nif, Password = pass }, out regante)) {
                error = "Usuario o contraseña no válidos";
                return false;
            }
            if (regante == null || regante.Role != "admin") {
                error = "Esta operación requiere permisos de administrador";
                return false;
            }
            return true;
        }

        public IActionResult Importacion() {
            ViewBag.Title = "Importación para la creación masica de unidades de cultivo.";
            return View();
        }

        public IActionResult ImportacionAnalisisSuelo() {
            ViewBag.Title = "Importación masiva de análisis de suelos";
            return View();
        }

        public IActionResult EspecificacionesImportacion() {
            return View();
        }

        public IActionResult EspecificacionesimportacionAnalisisSuelo() {
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
                var nif = Request.Form["NifRegante"].ToString();
                var pass = Request.Form["PassRegante"].ToString();
                string error;
                if (!CredencialesAdminValidas(nif, pass, out error)) {
                    Log.Aviso("Intento de importación de mapas sin credenciales válidas. Usuario indicado: " + nif, null);
                    return error;
                }

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
                Log.Info("Importado mapa versión " + idVersion + " nivel " + nivel + " por " + nif);
                return "OK";
            } catch (Exception ex) {
                Log.Error("Importación de mapas", ex);
                return "Error en la importación. Consulte el registro del servidor.";
            } finally {
                // El temporal se borraba sólo cuando todo iba bien; ante cualquier error quedaba huérfano.
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
                var nif = Request.Form["NifRegante"].ToString();
                var pass = Request.Form["PassRegante"].ToString();
                string error;
                if (!CredencialesAdminValidas(nif, pass, out error)) {
                    Log.Aviso("Intento de eliminación de mapas sin credenciales válidas. Usuario indicado: " + nif, null);
                    return error;
                }

                var idVersion = Request.Form["paramJson[idVersion]"].ToString();
                int nivel;
                if (!int.TryParse(Request.Form["paramJson[nivel]"].ToString(), out nivel))
                    return "El nivel indicado no es válido";

                DB.EliminarMapas(idVersion, nivel);
                Log.Info("Eliminado mapa versión " + idVersion + " nivel " + nivel + " por " + nif);
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

                var nif = Request.Form["NifRegante"].ToString();
                var pass = Request.Form["PassRegante"].ToString();
                Models.Regante regante;
                if (!DB.IsCorrectPassword(new Models.LoginRequest { NifRegante = nif, Password = pass }, out regante)) {
                    var lErr = new List<ErrorItem> {
                        new ErrorItem{NLinea=0, Descripcion="Usuario o contraseña no válidos" }
                    };
                    return Newtonsoft.Json.JsonConvert.SerializeObject(lErr);
                }

                List<ImportItemUCExcel> excel;
                using (var flujo = fichero.OpenReadStream()) {
                    excel = MiniExcelLibs.MiniExcel.Query<ImportItemUCExcel>(flujo).ToList();
                }

                int nImportados;
                var lErrores = ImportarUcFromExcel(nif, pass, excel, out nImportados);
                if (lErrores.Count > 0)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(lErrores);
                else {
                    CacheDatosHidricos.RecalculaSuelos();
                    Log.Info("Importadas " + nImportados + " unidades de cultivo por " + nif);
                    return "OK:" + nImportados.ToString();
                }
            } catch (Exception ex) {
                Log.Error("Importación de unidades de cultivo desde Excel", ex);
                return "Error: no se pudo completar la importación. Consulte el registro del servidor.";
            }
        }
    }

}
