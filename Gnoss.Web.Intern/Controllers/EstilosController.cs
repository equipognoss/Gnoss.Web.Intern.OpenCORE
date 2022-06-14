using Es.Riam.Gnoss.FileManager;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.InterfacesOpenArchivos;
using Es.Riam.Util;
using Gnoss.Web.Intern.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Gnoss.Web.Intern.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class EstilosController : ControllerBase
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
        private ConfigService _configService;
        private IHostingEnvironment _env;
        private IHttpContextAccessor _httpContextAccessor;
        private LoggingService _loggingService;
        private GestionArchivos mGestorArchivosOntologias;
        private FileOperationsService _fileOperationsService;
        private IUtilArchivos _utilArchivos;

        #endregion

        public EstilosController(LoggingService loggingService, IHttpContextAccessor httpContextAccessor, IHostingEnvironment env, ConfigService configService, IUtilArchivos utilArchivos)
        {
            _configService = configService;
            _loggingService = loggingService;
            _httpContextAccessor = httpContextAccessor;
            mRutaImagenes = Path.Combine(env.ContentRootPath, UtilArchivos.ContentImagenes);
            mRutaOntologias = Path.Combine(env.ContentRootPath, UtilArchivos.ContentOntologias);
            _env = env;
            _fileOperationsService = new FileOperationsService(_loggingService, _env);

            mRutaZipExe = Path.Combine(env.ContentRootPath, "/zip");

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
            mGestorArchivos = new GestionArchivos(loggingService, utilArchivos, pRutaArchivos: mRutaImagenes, pAzureStorageConnectionString: mAzureStorageConnectionString);
            mGestorArchivosOntologias = new GestionArchivos(loggingService, utilArchivos, pRutaArchivos: mRutaOntologias, pAzureStorageConnectionString: mAzureStorageConnectionStringOntologias);
        }

        #region Metodos
        

        [HttpPost]
        [Route("AgregarZIP")]
        public ActionResult AgregarZIP(string pNombre, string pExtension, Guid? pProyectoID, string pFecha, IFormFile file, string pNombreVersion = null, string pNombreCarpeta = null)
        {
            string personalizacion = (pProyectoID.HasValue ? pProyectoID.Value.ToString() : "ecosistema");

            personalizacion = (string.IsNullOrEmpty(pNombreCarpeta) ? personalizacion : pNombreCarpeta);

            Byte[] pFichero = _fileOperationsService.ReadFileBytes(file);
            // Ruta archivos en uso
            string ruta;
            // Ruta raiz de historial
            string rutaRaizVersiones;
            // Ruta del historico que se esta subiendo
            string rutaVersion;
            if (string.IsNullOrEmpty(pNombreVersion))
            {
                ruta = Path.Combine("proyectos", "personalizacion", personalizacion);
                rutaRaizVersiones = Path.Combine("proyectos", "personalizacion", personalizacion, "historial");
                rutaVersion = Path.Combine("proyectos", "personalizacion", personalizacion, "historial", pFecha);
            }
            else
            {
                ruta = Path.Combine("proyectos", "personalizacion", personalizacion, "versiones", pNombreVersion);
                rutaRaizVersiones = Path.Combine("proyectos", "personalizacion", personalizacion, "versiones", pNombreVersion, "historial" );
                rutaVersion = Path.Combine("proyectos", "personalizacion", personalizacion, "versiones", pNombreVersion, "historial", pFecha);
            }
            try
            {
                if (pExtension.ToLower().Equals(".zip") || pExtension.ToLower().Equals(".nupkg"))
                {
                    if (mGestorArchivos.ComprobarExisteDirectorio(ruta).Result)
                    {
                        CrearCopiaInicial(rutaRaizVersiones, ruta, pFecha);
                    }

                    mGestorArchivos.CrearFicheroFisico(rutaVersion, pNombre + pExtension, pFichero);

                    string rutaFichero = Path.Combine(mRutaImagenes, rutaVersion, pNombre + pExtension);
                    string rutaDescomprimir = Path.Combine(mRutaImagenes, ruta);

                    //if (string.IsNullOrEmpty(mAzureStorageConnectionString))
                    //{
                    //    UtilZip.Descomprimir(rutaFichero, rutaDescomprimir);
                    //}
                    //else
                    //{
                    //    mGestorArchivos.Descomprimir(pFichero, ruta);
                    //}

                    string rutaZip = mGestorArchivos.ObtenerRutaDirectorioZip(ruta);
                    ProcessStartInfo procStartInfo = new ProcessStartInfo(Path.Combine(mRutaZipExe, "7z.exe"), $" x -y \"{Path.Combine("historial",pFecha,pNombre + pExtension)}\"");
                    procStartInfo.RedirectStandardOutput = true;
                    procStartInfo.WorkingDirectory = rutaZip;
                    procStartInfo.UseShellExecute = false;
                    procStartInfo.CreateNoWindow = false;
                    //Inicializa el proceso
                    Process proc = new Process();
                    proc.StartInfo = procStartInfo;
                    proc.Start();
                    while (!proc.StandardOutput.EndOfStream)
                    {
                        string line = proc.StandardOutput.ReadLine();
                    }

                    if (pExtension.Equals(".nupkg"))
                    {

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
                    }

                    return Content("OK");
                }
                else
                {
                    return Content("ERROR");
                }
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
            {}
        }

        [HttpPost]
        [Route("DescargarZIP")]
        public ActionResult DescargarZIP(Guid? pProyectoID, string pNombreVersion = null, string pNombreCarpeta = null, bool pCss = false, bool pImagenes = false)
        {

            string personalizacion = (pProyectoID.HasValue ? pProyectoID.Value.ToString() : "ecosistema");

            //GuardarLogTest("Entra peticion descargaZip Estilos");

            personalizacion = (string.IsNullOrEmpty(pNombreCarpeta) ? personalizacion : pNombreCarpeta);

            byte[] respuesta = null;
            // Ruta archivos en uso
            string ruta;

            if (string.IsNullOrEmpty(pNombreVersion))
            {
                ruta = Path.Combine("proyectos", "personalizacion", personalizacion);
            }
            else
            {
                ruta = Path.Combine("proyectos", "personalizacion", personalizacion, "versiones", pNombreVersion);
            }

            //GuardarLogTest("La ruta donde estaran los estilos es la siguiente: " + mRutaImagenes + "\\" + ruta);

            try
            {

                AzureStorage azureStorage = null;
                if (!string.IsNullOrEmpty(mAzureStorageConnectionString))
                {
                    GuardarLogTest("La cadena de conexion a Azure es: " + mAzureStorageConnectionString);
                    azureStorage = new AzureStorage(mAzureStorageConnectionString);
                }
                string[] rutasArchivos = mGestorArchivos.ObtenerFicherosDeDirectorioYSubDirectorios(ruta).Result;
                using (var ms = new MemoryStream())
                {
                    using (var zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                    {
                        foreach (string archivo in rutasArchivos)
                        {
                            if ((!archivo.StartsWith("versiones") && !archivo.StartsWith("historial") && !archivo.StartsWith("cms")) && ((!pCss && !pImagenes) || (!(!pCss && !pImagenes) && ((pCss && !CoomprobarFicheroMultimedia(archivo)) || (pImagenes && CoomprobarFicheroMultimedia(archivo))))))
                            {
                                if (azureStorage == null)
                                {
                                    byte[] bytes = System.IO.File.ReadAllBytes(Path.Combine(mRutaImagenes, ruta, archivo));
                                    var entry = zipArchive.CreateEntry(archivo);
                                    using (Stream s = entry.Open())
                                    {
                                        if (bytes.Length <= 50 * 1024 * 1024)
                                        {
                                            s.Write(bytes, 0, bytes.Length);
                                            s.Flush();
                                        }
                                    }
                                    entry = null;
                                    bytes = null;
                                }
                                else
                                {
                                    byte[] bytes = azureStorage.DescargarDocumentoSubdirectorios(ruta, archivo).Result;
                                    var entry = zipArchive.CreateEntry(archivo);
                                    using (Stream s = entry.Open())
                                    {
                                        if (bytes.Length <= 50 * 1024 * 1024)
                                        {
                                            s.Write(bytes, 0, bytes.Length);
                                            s.Flush();
                                        }
                                    }
                                    entry = null;
                                    bytes = null;
                                }
                            }
                        }
                    }
                    ms.Flush();

                    respuesta = ms.ToArray();
                }

                rutasArchivos = null;

                return File(respuesta, "application/zip");

            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
            }

            return null;
        }

        [NonAction]
        private bool CoomprobarFicheroMultimedia(string archivo)
        {
            bool esValido = false;
            if (archivo.EndsWith(".jpeg") || archivo.EndsWith(".jpg") || archivo.EndsWith(".jfif") || archivo.EndsWith(".exif") || archivo.EndsWith(".tiff") || archivo.EndsWith(".gif") || archivo.EndsWith(".png") || archivo.EndsWith(".ppm") || archivo.EndsWith(".pgm") || archivo.EndsWith(".pbm") || archivo.EndsWith(".pnm") || archivo.EndsWith(".hdr") || archivo.EndsWith(".webp") || archivo.EndsWith(".img") || archivo.EndsWith(".ico") || archivo.EndsWith(".bat") || archivo.EndsWith(".bpg") || archivo.EndsWith(".svg") || archivo.EndsWith(".webm") || archivo.EndsWith(".flv") || archivo.EndsWith(".mkv") || archivo.EndsWith(".vob") || archivo.EndsWith(".ogv") || archivo.EndsWith(".drc") || archivo.EndsWith(".ogg") || archivo.EndsWith(".gif") || archivo.EndsWith(".mng") || archivo.EndsWith(".gifv") || archivo.EndsWith(".avi") || archivo.EndsWith(".mts") || archivo.EndsWith(".m2ts") || archivo.EndsWith(".ts") || archivo.EndsWith(".mov") || archivo.EndsWith(".qt") || archivo.EndsWith(".wmv") || archivo.EndsWith(".yuv") || archivo.EndsWith(".rmvb") || archivo.EndsWith(".rm") || archivo.EndsWith(".asf") || archivo.EndsWith(".amv") || archivo.EndsWith(".m4p") || archivo.EndsWith(".mp4") || archivo.EndsWith(".m4v") || archivo.EndsWith(".mpg") || archivo.EndsWith(".mp2") || archivo.EndsWith(".mpeg") || archivo.EndsWith(".mpe") || archivo.EndsWith(".mpv") || archivo.EndsWith(".mpg") || archivo.EndsWith(".mpeg") || archivo.EndsWith(".m2v") || archivo.EndsWith(".svi") || archivo.EndsWith(".3gp") || archivo.EndsWith(".3g2") || archivo.EndsWith(".mxf") || archivo.EndsWith(".roq") || archivo.EndsWith(".nsv"))
            {
                return true;
            }
            return esValido;
        }

        //Metodo para devolver los archivos que sean de Estilo Web (css,js,fonts etc...)
        [HttpPost]
        [Route("DescargarZIPCss")]
        public ActionResult DescargarZIPCss(Guid? pProyectoID, string pNombreVersion = null, string pNombreCarpeta = null)
        {
            return DescargarZIP(pProyectoID, pNombreVersion, pNombreCarpeta, true, false);
        }

        //Metodo para devolver los archivos que sean de Estilo Imagenes (jpg,png,gif etc...)
        [HttpPost]
        [Route("DescargarZIPImagenes")]
        public ActionResult DescargarZIPImagenes(Guid? pProyectoID, string pNombreVersion = null, string pNombreCarpeta = null)
        {
            return DescargarZIP(pProyectoID, pNombreVersion, pNombreCarpeta, false, true);
        }


        /// <summary>
        /// Crea una copia de seguridad de los archivos iniciales.
        /// </summary>
        /// <param name="pRutaRaizHistorial">Ruta donde se guardara el historial de subidas</param>
        /// <param name="pDirectorio">Directorio del que se quiere realizar la copia</param>
        /// <param name="pFecha">Fecha de la subida</param>
        /// <returns></returns>
        [NonAction]
        private void CrearCopiaInicial(string pRutaRaizHistorial, string pDirectorio, string pFecha)
        {
            // Si no existe el directorio de historial, creamos una copia del contenido original
            if (!mGestorArchivos.ComprobarExisteDirectorio(pRutaRaizHistorial).Result)
            {
                string rutaVersionInicial = Path.Combine(pRutaRaizHistorial, "_inicial");

                if (string.IsNullOrEmpty(mAzureStorageConnectionString))
                {
                    // No hay azure, podemos comprimir:
                    string[] archivos = mGestorArchivos.ObtenerFicherosDeDirectorio(pDirectorio).Result;
                    string[] directorios = mGestorArchivos.ObtenerSubdirectoriosDeDirectorio(pDirectorio).Result;

                    string[] rutasArchivos = new string[archivos.Length + directorios.Length];
                    for (int i = 0; i < archivos.Length; i++)
                    {
                        rutasArchivos[i] = Path.Combine(mRutaImagenes, pDirectorio, archivos[i]);
                    }
                    for (int i = archivos.Length; i < rutasArchivos.Length; i++)
                    {
                        if (!directorios[i - archivos.Length].Equals("versiones"))
                        {
                            rutasArchivos[i] = Path.Combine(mRutaImagenes, pDirectorio, directorios[i - archivos.Length]);
                        }
                    }

                    mGestorArchivos.CrearDirectorioFisico(rutaVersionInicial);

                    UtilZip.Comprimir(rutasArchivos, Path.Combine(mRutaImagenes, rutaVersionInicial, "version_inicial.zip"));
                }
                else
                {
                    // Hay azure, copiamos los archivos en otra carpeta: 
                    mGestorArchivos.CopiarArchivosDeDirectorio(pDirectorio, rutaVersionInicial, true);
                }

            }
        }

        #endregion


        [HttpPost]
        [Route("DescargarRutasFicheros")]
        public ActionResult DescargarRutasFicheros(Guid? pProyectoID, string pNombreVersion = null, string pNombreCarpeta = null)
        {

            string personalizacion = (pProyectoID.HasValue ? pProyectoID.Value.ToString() : "ecosistema");

            //GuardarLogTest("Entra peticion descargaZip Estilos");

            personalizacion = (string.IsNullOrEmpty(pNombreCarpeta) ? personalizacion : pNombreCarpeta);

            List<string> respuesta = new List<string>();
            // Ruta archivos en uso
            string ruta;


            if (string.IsNullOrEmpty(pNombreVersion))
            {
                ruta = Path.Combine("proyectos", "personalizacion", personalizacion);
            }
            else
            {
                ruta = Path.Combine("proyectos", "personalizacion", personalizacion, "versiones", pNombreVersion);
            }

            //GuardarLogTest("La ruta donde estaran los estilos es la siguiente: " + mRutaImagenes + "\\" + ruta);

            try
            {

                AzureStorage azureStorage = null;
                if (!string.IsNullOrEmpty(mAzureStorageConnectionString))
                {
                    GuardarLogTest("La cadena de conexion a Azure es: " + mAzureStorageConnectionString);
                    azureStorage = new AzureStorage(mAzureStorageConnectionString);
                }
                string[] rutasArchivos = mGestorArchivos.ObtenerFicherosDeDirectorioYSubDirectorios(ruta).Result;
                using (var ms = new MemoryStream())
                {
                    using (var zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                    {
                        foreach (string archivo in rutasArchivos)
                        {
                            if (!archivo.StartsWith("versiones") && !archivo.StartsWith("historial") && !archivo.StartsWith("cms"))
                            {
                                respuesta.Add(Path.Combine(mRutaImagenes, ruta, archivo));
                            }
                        }
                    }
                    ms.Flush();
                }

                rutasArchivos = null;

                return Ok(respuesta);

            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
            }

            return null;
        }
    }
}
