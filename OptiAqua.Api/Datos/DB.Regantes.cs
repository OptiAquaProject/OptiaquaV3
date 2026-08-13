namespace DatosOptiaqua {
    using Models;
    using NPoco;
    using Org.BouncyCastle.Crypto.Signers;
    using System;
    using System.Collections.Generic;
    using System.Data.SqlTypes;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using webapi;
    using webapi.Utiles;

    /// <summary>
    /// Capa de acceso a datos de OptiAqua sobre SQL Server (librería NPoco).
    /// Regantes y asesores: ficha, alta y modificación, contraseñas y las
    /// comprobaciones de autorización sobre parcelas y unidades de cultivo.
    /// DB es una clase parcial repartida por dominios; dentro de cada fichero
    /// los miembros van en orden alfabético.
    /// </summary>
    public static partial class DB {

        internal static List<int> AsesorParcelaList(int idUsuario) {
            Database db = DB.ConexionOptiaqua;
            return db.Fetch<int>("select IdParcelaInt from AsesorParcelas where IdAsesor=@0 ", idUsuario);
        }

        /// <summary>
        /// The AsesorUnidadCultivoList.
        /// </summary>
        /// <param name="idUsuario">The idUsuario<see cref="int"/>.</param>
        /// <returns>The <see cref="List{string}"/>.</returns>
        internal static List<string> AsesorUnidadCultivoList(int idUsuario) {
            Database db = DB.ConexionOptiaqua;
            return db.Fetch<string>("select IdUnidadCultivo from AsesorUnidadCultivo where idRegante=@0", idUsuario);
        }

        internal static string AsesorUnidadCultivoSave(int idRegante, List<string> lUnidadesCultivo) {
            Database db = DB.ConexionOptiaqua;
            Regante regante = db.SingleById<Regante>(idRegante);
            if (regante.Role != "asesor")
                return "El regante indicado no tienen role de asesor";
            db.Delete<AsesorUnidadCultivo>("wHERE IDREGANTE=@0", idRegante);
            foreach (string iduc in lUnidadesCultivo) {
                AsesorUnidadCultivo reg = new AsesorUnidadCultivo { IdRegante = idRegante, IdUnidadCultivo = iduc };
                db.Insert(reg);
            }
            return "Eliminada anterior lista de unidades de cultivo, se ha creado una nueva con las " + lUnidadesCultivo.Count + " unidades de cultivo indicadas";
        }

        /// <summary>
        /// Crear contraseña encriptada a partir del nif del regante.
        /// </summary>
        /// <param name="nifRegante">nifRegante<see cref="string"/>.</param>
        /// <param name="password">password<see cref="string"/>.</param>
        /// <returns><see cref="string"/>.</returns>
        public static string BuildPassword(string nifRegante, string password) {
            if (string.IsNullOrEmpty(nifRegante)) {
                nifRegante = "0000000000000";
            }
            string cpass = Encriptacion.XorIt(nifRegante, password) + "0000000000000";
            cpass = cpass.Substring(1, 12); // 12 como máximo            
            string ret = Encriptacion.Encripta(cpass);
            return ret;
        }

        /// <summary>
        /// The EstaAutorizado.
        /// </summary>
        /// <param name="idUsuario">The idUsuario<see cref="int"/>.</param>
        /// <param name="role">The role<see cref="string"/>.</param>
        /// <param name="idUnidadCultivo">The idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idTemporada">The idTemporada<see cref="string"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        internal static bool EstaAutorizado(int idUsuario, string role, string idUnidadCultivo, string idTemporada) {
            if (role == "admin")
                return true;
            if (role == "asesor") {
                List<string> lAsesor = DB.AsesorUnidadCultivoList(idUsuario);
                return lAsesor.Contains(idUnidadCultivo);
            }
            if (role == "dbo")
                return DB.LaUnidadDeCultivoPerteneceAlReganteEnLaTemporada(idUnidadCultivo, idUsuario, idTemporada);
            return false;
        }

        /// <summary>
        /// The EstaAutorizado.
        /// </summary>
        /// <param name="idUsuario">The idUsuario<see cref="int"/>.</param>
        /// <param name="role">The role<see cref="string"/>.</param>
        /// <param name="idUnidadCultivo">The idUnidadCultivo<see cref="string"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        internal static bool EstaAutorizado(int idUsuario, string role, string idUnidadCultivo) {
            if (role == "admin")
                return true;
            if (role == "asesor") {
                List<string> lAsesor = DB.AsesorUnidadCultivoList(idUsuario);
                return lAsesor.Contains(idUnidadCultivo);
            }
            if (role == "dbo")
                return DB.LaUnidadDeCultivoPerteneceAlRegante(idUnidadCultivo, idUsuario);
            return false;
        }

        /// <summary>
        /// The EstaAutorizado.
        /// </summary>
        /// <param name="idUsuario">The idUsuario<see cref="int"/>.</param>
        /// <param name="role">The role<see cref="string"/>.</param>
        /// <param name="idParcela">The idParcela<see cref="int"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        internal static bool EstaAutorizado(int idUsuario, string role, int idParcela) {
            if (role == "admin")
                return true;
            if (role == "asesor") {
                List<int> lAsesor = DB.AsesorParcelaList(idUsuario);
                return lAsesor.Contains(idParcela);
            }
            if (role == "dbo")
                return DB.LaParcelaPerteneceAlRegante(idUsuario, idParcela);
            return false;
        }

        /// <summary>
        /// The IsCorrectPassword.
        /// </summary>
        /// <param name="login">The login<see cref="LoginRequest"/>.</param>
        /// <param name="regante">The regante<see cref="Regante"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public static bool IsCorrectPassword(LoginRequest login, out Regante regante) {
            regante = null;
            try {
                Database db = DB.ConexionOptiaqua;
                regante = db.SingleOrDefault<Regante>("select * from regante where nif=@0", login.NifRegante);
                if (regante == null)
                    return false;
                string pass1 = BuildPassword(login.NifRegante, login.Password);
                return regante.Contraseña == pass1;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Retorna si el password es correcto para en nif indicado.
        /// </summary>
        /// <param name="nif">The nif<see cref="string"/>.</param>
        /// <param name="password">The password<see cref="string"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public static bool IsCorrectPassword(string nif, string password) {
            try {
                Database db = DB.ConexionOptiaqua;
                Regante regante = db.SingleOrDefault<Regante>("select * from regante where nif=@0", nif);
                if (regante == null)
                    return false;
                string pass1 = BuildPassword(nif, password);
                return regante.Contraseña == pass1;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// The LaParcelaPerteneceAlRegante.
        /// </summary>
        /// <param name="idParcela">The idParcela<see cref="int"/>.</param>
        /// <param name="idUsuario">The idUsuario<see cref="int"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public static bool LaParcelaPerteneceAlRegante(int idParcela, int idUsuario) {
            Database db = DB.ConexionOptiaqua;
            string sql = "select idParcelaInt from Parcela where IdParcela=@0 and IdRegante=@1";
            bool pertenece = db.SingleOrDefault<int?>(sql, idParcela, idUsuario) != null;
            if (pertenece)
                return true;
            sql = "select idParcelaInt from UnidadCultivoParcela where IdParcelaInt=@0 and IdRegante=@1";
            pertenece = db.SingleOrDefault<int?>(sql, idParcela, idUsuario) != null;
            return pertenece;
        }

        /// <summary>
        /// LaUnidadDeCultivoPerteneceAlRegante.
        /// </summary>
        /// <param name="IdUnidadCultivo">IdUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idRegante">idRegante<see cref="int"/>.</param>
        /// <param name="IdTemporada">IdTemporada<see cref="string"/>.</param>
        /// <returns><see cref="bool"/>.</returns>
        public static bool LaUnidadDeCultivoPerteneceAlRegante(string IdUnidadCultivo, int idRegante, string IdTemporada) {
            Database db = DB.ConexionOptiaqua;
            string sql = "Select IdRegante from UnidadCultivoParcela Where IdUnidadCultivo=@0 and IdTemporada=@1 and idRegante=@2";
            List<object> lu = db.Fetch<object>(sql, IdUnidadCultivo, IdTemporada, idRegante);
            return lu.Count != 0;
        }

        /// <summary>
        /// LaUnidadDeCultivoPerteneceAlRegante.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idRegante">idRegante<see cref="int"/>.</param>
        /// <returns><see cref="bool"/>.</returns>
        public static bool LaUnidadDeCultivoPerteneceAlRegante(string idUnidadCultivo, int idRegante) {
            Database db = DB.ConexionOptiaqua;
            UnidadCultivo unidadCultivo = db.SingleOrDefaultById<UnidadCultivo>(idUnidadCultivo);
            if (unidadCultivo == null)
                return false;
            return unidadCultivo.IdRegante == idRegante;
        }

        /// <summary>
        /// LaUnidadDeCultivoPerteneceAlReganteEnLaTemporada.
        /// </summary>
        /// <param name="idUnidadCultivo">idUnidadCultivo<see cref="string"/>.</param>
        /// <param name="idRegante">idRegante<see cref="int"/>.</param>
        /// <param name="idTemporada">idTemporada<see cref="string"/>.</param>
        /// <returns><see cref="bool"/>.</returns>
        public static bool LaUnidadDeCultivoPerteneceAlReganteEnLaTemporada(string idUnidadCultivo, int idRegante, string idTemporada) {
            Database db = DB.ConexionOptiaqua;
            string sql = "Select IdUnidadCultivo from UnidadCultivoCultivo where IdUnidadCultivo=@0 and IdRegante=@1 and idTemporada=@2";
            string unidadCultivo = db.SingleOrDefault<string>(sql, idUnidadCultivo, idRegante, idTemporada);
            return unidadCultivo != null;
        }

        /// <summary>
        /// PasswordSave.
        /// </summary>
        /// <param name="login">login<see cref="LoginRequest"/>.</param>
        /// <returns><see cref="bool"/>.</returns>
        public static bool PasswordSave(LoginRequest login) {
            Database db = DB.ConexionOptiaqua;
            Regante regante = db.SingleOrDefault<Regante>("Where NIF=@0", login.NifRegante);
            regante.Contraseña = BuildPassword(login.NifRegante, login.Password);
            db.Save(regante);
            return true;
        }

        /// <summary>
        /// Regante.
        /// </summary>
        /// <param name="idRegante">idRegante<see cref="int?"/>.</param>
        /// <returns><see cref="object"/>.</returns>
        public static object Regante(int? idRegante) {
            if (idRegante == null)
                return null;
            Database db = DB.ConexionOptiaqua;
            return db.SingleById<Regante>(idRegante);
        }

        /// <summary>
        /// ReganteList.
        /// </summary>
        /// <param name="strFecha">strFecha<see cref="string"/>.</param>
        /// <param name="IdRegante">IdRegante<see cref="string"/>.</param>
        /// <param name="IdUnidadCultivo">IdUnidadCultivo<see cref="string"/>.</param>
        /// <param name="IdParcela">IdParcela<see cref="string"/>.</param>
        /// <param name="Search">Search<see cref="string"/>.</param>
        /// <returns><see cref="object"/>.</returns>
        public static object ReganteList(string strFecha, string IdRegante, string IdUnidadCultivo, string IdParcela, string Search) {
            Database db = DB.ConexionOptiaqua;
            string idTemporada = DB.TemporadaDeFecha(IdUnidadCultivo, DateTime.Parse(strFecha));
            if (idTemporada == null)
                idTemporada = TemporadaActiva();
            IdUnidadCultivo = IdUnidadCultivo.Quoted();
            Search = Search.Quoted();
            string sql = $"SELECT * FROM ReganteList('{idTemporada}',{IdRegante},{IdUnidadCultivo},{IdParcela},{Search})";
            return db.Fetch<object>(sql);
        }

        /// <summary>
        /// RegantesList.
        /// </summary>
        /// <returns><see cref="object"/>.</returns>
        public static object RegantesList() {
            Database db = DB.ConexionOptiaqua;
            string sql = "Select IdRegante, Nombre, Telefono, TelefonoSMS, Email from Regante";
            return db.Fetch<object>(sql);
        }

        /// <summary>
        /// ReganteUpdate.
        /// Retorna la clave del cliente
        /// Si idRegane =-1 se creará nuevo
        /// </summary>
        /// <param name="rp">rp<see cref="RegantePost"/>.</param>
        public static string ReganteUpdate(RegantePost rp) {
            Database db = DB.ConexionOptiaqua;
            Regante regante = new Regante {
                NIF = rp.NIF,
                IdRegante = rp.IdRegante,
                IdGadmin = rp.IdGadmin,
                Nombre = rp.Nombre,
                Direccion = rp.Direccion,
                CodigoPostal = rp.CodigoPostal,
                Poblacion = rp.Poblacion,
                Provincia = rp.Provincia,
                Pais = rp.Pais,
                Telefono = rp.Telefono,
                TelefonoSMS = rp.TelefonoSMS,
                Email = rp.Email,
                Role = rp.Role,
                WebActive = true
            };
            if (regante.Role != "dbo" && regante.Role != "admin" && regante.Role != "asesor") {
                return "Error. El role puede ser:dbo,admin,asesor";
            }

            // La contraseña sólo se genera en el alta. En una actualización se conserva la que
            // ya tuviera el regante: antes, cualquier edición de la ficha (un teléfono, un correo)
            // le reseteaba la contraseña a "Pass"+IdRegante -un valor que se adivina- y además
            // la devolvía en la respuesta.
            Regante existente = db.SingleOrDefaultById<Regante>(rp.IdRegante);
            if (existente == null) {
                regante.Contraseña = BuildPassword(regante.NIF, "Pass" + regante.IdRegante.ToString());
                db.Save(regante);
                Log.Info("Alta de regante " + regante.IdRegante + " (" + regante.NIF + ")");
                return "Pass" + regante.IdRegante.ToString();
            }
            regante.Contraseña = existente.Contraseña;
            db.Save(regante);
            Log.Info("Actualización de la ficha del regante " + regante.IdRegante + " (" + regante.NIF + ")");
            return "OK";
        }
    }
}
