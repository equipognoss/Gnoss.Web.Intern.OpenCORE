using Es.Riam.Gnoss.FileManager;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.InterfacesOpenArchivos;
using Es.Riam.Util;
using Gnoss.Web.Intern.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gnoss.Web.Intern.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class ObjetosMultimediaController : ControllerBase
    {
        #region Miembros

        /// <summary>
        /// Almacena la ruta de las imagenes
        /// </summary>
        private static string mRutaImagenes = null;

        /// <summary>
        /// Almacenamos la ruta a las ontologías.
        /// </summary>
        private static string mRutaOntologias = null;

        /// <summary>
        /// Almacenamos la ruta del fichero del zip
        /// </summary>
        private static string mRutaZipExe = null;

        private static string mAzureStorageConnectionString;
        private static string mAzureStorageConnectionStringOntologias = "";

        private GestionArchivos mGestorArchivos;
        private LoggingService _loggingService;
        private IHttpContextAccessor _httpContextAccessor;

        private IHostingEnvironment _env;
        private ConfigService _configService;
        private FileOperationsService _fileOperationsService;
        private IUtilArchivos _utilArchivos;

        #endregion
        public ObjetosMultimediaController(LoggingService loggingService, IHttpContextAccessor httpContextAccessor, IHostingEnvironment env, ConfigService configService, IUtilArchivos utilArchivos)
        {
            _loggingService = loggingService;
            _httpContextAccessor = httpContextAccessor;
            _env = env;
            _configService = configService;

            mRutaImagenes = configService.GetRutaImagenes();
            if (string.IsNullOrEmpty(mRutaImagenes))
            {
                mRutaImagenes = Path.Combine(env.ContentRootPath, UtilArchivos.ContentImagenes);
            }

            mRutaOntologias = configService.GetRutaOntologias();
            if (string.IsNullOrEmpty(mRutaOntologias))
            {
                mRutaOntologias = Path.Combine(env.ContentRootPath, UtilArchivos.ContentOntologias);
            }

            string rutaConfigs = Path.Combine(env.ContentRootPath, "config");
            mRutaZipExe = Path.Combine(env.ContentRootPath, "zip");


            mAzureStorageConnectionString = _configService.ObtenerAzureStorageConnectionString();
            if (!string.IsNullOrEmpty(mAzureStorageConnectionString))
            {
                mAzureStorageConnectionStringOntologias = mAzureStorageConnectionString + "/" + UtilArchivos.ContentOntologias;
                mAzureStorageConnectionString += "/" + UtilArchivos.ContentImagenes;
            }
            else if (mAzureStorageConnectionString == null)
            {
                mAzureStorageConnectionString = "";
            }
            _utilArchivos = utilArchivos;
            mGestorArchivos = new GestionArchivos(_loggingService, utilArchivos, pRutaArchivos: mRutaImagenes, pAzureStorageConnectionString: mAzureStorageConnectionString);
            _fileOperationsService = new FileOperationsService(_loggingService, _env);

        }
        #region Metodos

        [HttpPost]
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("AgregarZIP")]
        public ActionResult AgregarZIP(Guid? pProyectoID, IFormFile file, string pNombreCarpeta = null)
        {

            Byte[] pFichero = _fileOperationsService.ReadFileBytes(file);
            _fileOperationsService.GuardarLogError("Entra en el AgregarZIP");

            string personalizacion = (pProyectoID.HasValue ? pProyectoID.Value.ToString() : "ecosistema");
            personalizacion = (string.IsNullOrEmpty(pNombreCarpeta) ? personalizacion : pNombreCarpeta);

            // Ruta archivos en uso
            string ruta = Path.Combine("proyectos", "personalizacion", personalizacion, "cms");

            //Lo eliminamos por si no se ha eliminado el del anterior proceso
            mGestorArchivos.EliminarFicheroFisico(ruta, "objetosMultimedia.zip");

            string rutaZip = mGestorArchivos.ObtenerRutaDirectorioZip(ruta);
            _fileOperationsService.GuardarLogError("Empieza el proceso de copiado del fichero");
            _fileOperationsService.GuardarLogError("Bytes del fichero: " + pFichero.Length);

            try
            {
                mGestorArchivos.CrearFicheroFisico(ruta, "objetosMultimedia.zip", pFichero);
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return Content("ERROR");
            }

            try
            {
                FileInfo archivo = new FileInfo(Path.Combine(rutaZip, "objetosMultimedia.zip"));
                _fileOperationsService.GuardarLogError("Fichero copiado");

                if (archivo.Exists)
                {
                    _fileOperationsService.GuardarLogError("Bytes del fichero: " + archivo.Length);
                }
                //WIN
                //System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(Path.Combine(mRutaZipExe, "7z.exe"), $" x -y objetosMultimedia.zip");
                //LINUX
                System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo("unzip", $" -o objetosMultimedia.zip");
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.WorkingDirectory = rutaZip;
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.RedirectStandardError = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = false;
                StringBuilder sb = new StringBuilder();

                //Inicializa el proceso
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                while (!proc.StandardOutput.EndOfStream)
                {
                    sb.AppendLine(proc.StandardOutput.ReadLine());
                }

                if (proc.ExitCode != 0)
                {
                    while (!proc.StandardError.EndOfStream)
                    {
                        sb.AppendLine("Error: ");
                        sb.AppendLine(proc.StandardError.ReadLine());
                    }
                    //throw new Exception($"Error al ejecutar el nuget.exe con la instrucción: \n\t\"{proc.StartInfo.Arguments}\". Respuesta: {sb.ToString()}");
                }
                _fileOperationsService.GuardarLogError(sb.ToString());
                _fileOperationsService.GuardarLogError("Descomprimido");

                _fileOperationsService.GuardarLogError("Eliminar los ficheros");
                mGestorArchivos.EliminarFicheroFisico(ruta, "objetosMultimedia.zip");

                if (Directory.Exists(Path.Combine(mRutaImagenes, ruta, "_rels")))
                {
                    Directory.Delete(Path.Combine(mRutaImagenes, ruta, "_rels"), true);
                }
                if (Directory.Exists(Path.Combine(mRutaImagenes, ruta, "package")))
                {
                    Directory.Delete(Path.Combine(mRutaImagenes, ruta, "package"), true);
                }
                if (System.IO.File.Exists(Path.Combine(mRutaImagenes, ruta, "[Content_Types].xml")))
                {
                    System.IO.File.Delete(Path.Combine(mRutaImagenes, ruta, "[Content_Types].xml"));
                }
                string[] rutasArchivos = mGestorArchivos.ObtenerFicherosDeDirectorioYSubDirectorios(ruta).Result;
                foreach (string rutaArchivo in rutasArchivos)
                {
                    if (rutaArchivo.Contains(".nuspec"))
                    {
                        if (System.IO.File.Exists(Path.Combine(mRutaImagenes, ruta, rutaArchivo)))
                        {
                            System.IO.File.Delete(Path.Combine(mRutaImagenes, ruta, rutaArchivo));
                        }
                    }
                }

                return Content("OK");
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return Content("ERROR");
            }
        }

        [NonAction]
        public static void GuardarLogTest(string message)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "error_servicioInterno.txt"), true, System.Text.Encoding.Default))
                {
                    sw.WriteLine(Environment.NewLine + "Fecha: " + DateTime.Now + Environment.NewLine + Environment.NewLine);
                    // Escribo el error
                    sw.WriteLine(message);
                }
            }
            catch (Exception ex)
            {

            }
        }

        [HttpPost]
        [Route("DescargarZIP")]
        public ActionResult DescargarZIP(Guid? pProyectoID, string pNombreCarpeta = null)
        {
            byte[] respuesta = null;
            // Ruta archivos en uso
            string personalizacion = (pProyectoID.HasValue ? pProyectoID.Value.ToString() : "ecosistema");

            //GuardarLogTest("Entra peticion descargaZip ObjetosMultimedia");

            personalizacion = (string.IsNullOrEmpty(pNombreCarpeta) ? personalizacion : pNombreCarpeta);

            string ruta = Path.Combine("proyectos", "personalizacion", personalizacion, "cms");
            //GuardarLogTest("La ruta para la descarga de ObjetosMultimedia es: " + mRutaImagenes + "\\" + ruta);

            try
            {
                //AzureStorage azureStorage = null;
                //if (!string.IsNullOrEmpty(mAzureStorageConnectionString))
                //{
                //    GuardarLogTest("La cadena de conexion a Azure es: " + mAzureStorageConnectionString);
                //    azureStorage = new AzureStorage(mAzureStorageConnectionString);
                //}

                string rutaZip = mGestorArchivos.ObtenerRutaDirectorioZip(ruta);
                //WIN
                //System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(Path.Combine(mRutaZipExe, "7z.exe"), " a ObjetosMultimedia.zip");
                //LINUX
                ProcessStartInfo procStartInfo = new ProcessStartInfo("zip", $"-r ObjetosMultimedia.zip . -i *");
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.WorkingDirectory = rutaZip;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = false;
                //Inicializa el proceso
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                while (!proc.StandardOutput.EndOfStream)
                {
                    string line = proc.StandardOutput.ReadLine();
                }

                byte[] ficheroZip = System.IO.File.ReadAllBytes(Path.Combine(mRutaImagenes, ruta, "ObjetosMultimedia.zip"));

                respuesta = ficheroZip.ToArray();

                if (System.IO.File.Exists(Path.Combine(mRutaImagenes, ruta, "ObjetosMultimedia.zip")))
                {
                    System.IO.File.Delete(Path.Combine(mRutaImagenes, ruta, "ObjetosMultimedia.zip"));
                }
                return File(respuesta, "application/zip");
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return Content("ERROR");
            }
        }

      
        #endregion
    }
}
