using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.Facetado;
using Es.Riam.Gnoss.AD.Facetado.Model;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.CL.ServiciosGenerales;
using Es.Riam.Gnoss.Elementos.Documentacion;
using Es.Riam.Gnoss.Elementos.ParametroAplicacion;
using Es.Riam.Gnoss.FileManager;
using Es.Riam.Gnoss.Logica.Documentacion;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.UtilServiciosWeb;
using Es.Riam.Gnoss.Web.Controles.Documentacion;
using Es.Riam.Gnoss.Web.Controles.ParametroAplicacionGBD;
using Es.Riam.Gnoss.Web.Controles.ServicioImagenesWrapper;
using Es.Riam.Gnoss.Web.Controles.ServicioImagenesWrapper.Model;
using Es.Riam.Gnoss.Web.MVC.Models.AdministrarEstilos;
using Es.Riam.InterfacesOpenArchivos;
using Es.Riam.Util;
using Gnoss.Web.Intern.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Gnoss.Web.Intern.Controllers
{

    /// <summary>
    /// Servicio web de imágenes
    /// </summary>
    [ApiController]
    [Route("image-service")]
    [Authorize]
    public class ImagenesController : ControllerBase
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

        private static string mAzureStorageConnectionString;
        private static string mAzureStorageConnectionStringOntologias = "";

        private GestionArchivos mGestorArchivos;

        private GestionArchivos mGestorArchivosOntologias;

        private IHostingEnvironment mEnv;

        private LoggingService mLoggingService;

        private IHttpContextAccessor mHttpContextAccessor;
        private ConfigService _configService;
        private EntityContext _entityContext;
        private IServicesUtilVirtuosoAndReplication _servicesUtilVirtuosoAndReplication;
        private VirtuosoAD _virtuosoAD;
        private FileOperationsService _fileOperationsService;
        private IUtilArchivos _utilArchivos;
        private ILogger mlogger;
        private ILoggerFactory mLoggerFactory;
        #endregion

        #region Constructor

        public ImagenesController(LoggingService loggingService, IHostingEnvironment env, IHttpContextAccessor httpContextAccessor, ConfigService configService, IUtilArchivos utilArchivos, EntityContext entityContext, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication, VirtuosoAD virtuosoAD, ILogger<ImagenesController> logger, ILoggerFactory loggerFactory)
        {
            mHttpContextAccessor = httpContextAccessor;
            mEnv = env;
            mLoggingService = loggingService;
            _configService = configService;
            _entityContext = entityContext;
            _virtuosoAD = virtuosoAD;
            _servicesUtilVirtuosoAndReplication = servicesUtilVirtuosoAndReplication;
            mlogger = logger;
            mLoggerFactory = loggerFactory;
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
            _fileOperationsService = new FileOperationsService(mLoggingService, mEnv);
            //string rutaConfigs = Path.Combine(env.ContentRootPath, "config");


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
            mGestorArchivos = new GestionArchivos(loggingService, utilArchivos, mLoggerFactory.CreateLogger<GestionArchivos>(), mLoggerFactory, pRutaArchivos: mRutaImagenes, pAzureStorageConnectionString: mAzureStorageConnectionString);
            mGestorArchivosOntologias = new GestionArchivos(loggingService, utilArchivos, mLoggerFactory.CreateLogger<GestionArchivos>(), mLoggerFactory, pRutaArchivos: mRutaOntologias, pAzureStorageConnectionString: mAzureStorageConnectionStringOntologias);
        }

        #endregion

        #region Métodos web

        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("add-image")]
        public IActionResult AgregarImagen(GnossImage pImagen)
        {
            try
            {
                string[] nombreYDirectorio = mGestorArchivos.ObtenerDirectorioYArchivoDeNombreArchivo(pImagen.name);
                pImagen.relative_path = nombreYDirectorio[0];
                pImagen.name = nombreYDirectorio[1];

                EscribirImagen(pImagen);
                return Ok();
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Sube un archivo JS.
        /// </summary>
        /// <param name="pFichero">Contenido del fichero</param>
        /// <param name="pNombre">Nombre del fichero</param>
        /// <param name="pExtension">Extensión del fichero</param>
        /// <param name="pProyectoID">Identificador de la comunidad</param>
        /// <param name="pFecha">Fecha de la subida</param>
        /// <param name="pNombreVersion">Nombre de la nueva versión</param>
        /// <returns></returns>
        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("AgregarJS")]
        public IActionResult AgregarJS(Byte[] pFichero, string pNombre, string pExtension, Guid pProyectoID, string pFecha, string pNombreVersion = null)
        {
            // Ruta archivos en uso
            string ruta;
            // Ruta raiz de historial
            string rutaRaizVersiones;
            // Ruta del historico que se esta subiendo
            string rutaVersion;
            if (string.IsNullOrEmpty(pNombreVersion))
            {
                ruta = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString());
                rutaRaizVersiones = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "historial");
                rutaVersion = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "historial", pFecha);
            }
            else
            {
                ruta = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion);
                rutaRaizVersiones = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion, "historial");
                rutaVersion = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion, "historial", pFecha);
            }
            try
            {
                if (pExtension.ToLower().Equals(".js") && pNombre.Equals("community"))
                {

                    if (mGestorArchivos.ComprobarExisteDirectorio(ruta).Result)
                    {
                        CrearCopiaInicial(rutaRaizVersiones, ruta, pFecha);
                    }

                    mGestorArchivos.CrearFicheroFisico(ruta, pNombre + pExtension, pFichero);

                    mGestorArchivos.CrearFicheroFisico(rutaVersion, pNombre + pExtension, pFichero);

                    return Ok(true);
                }
                else
                {
                    return Ok(false);
                }
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return BadRequest(false);
            }
        }

        /// <summary>
        /// Sube un archivo CSS.
        /// </summary>
        /// <param name="pFichero">Contenido del fichero</param>
        /// <param name="pNombre">Nombre del fichero</param>
        /// <param name="pExtension">Extensión del fichero</param>
        /// <param name="pProyectoID">Identificador de la comunidad</param>
        /// <param name="pFecha">Fecha de la subida</param>
        /// <param name="pNombreVersion">Nombre de la nueva versión</param>
        /// <returns></returns>
        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("AgregarCSS")]
        public IActionResult AgregarCSS(Byte[] pFichero, string pNombre, string pExtension, Guid pProyectoID, string pFecha, string pNombreVersion = null)
        {
            // Ruta archivos en uso
            string ruta;
            // Ruta raiz de historial
            string rutaRaizVersiones;
            // Ruta del historico que se esta subiendo
            string rutaVersion;
            if (string.IsNullOrEmpty(pNombreVersion))
            {
                ruta = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString());
                rutaRaizVersiones = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "historial");
                rutaVersion = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "historial", pFecha);
            }
            else
            {
                ruta = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion);
                rutaRaizVersiones = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion, "historial");
                rutaVersion = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion, "historial", pFecha);
            }
            try
            {
                if (pExtension.ToLower().Equals(".css") && pNombre.Equals("community"))
                {
                    if (mGestorArchivos.ComprobarExisteDirectorio(ruta).Result)
                    {
                        CrearCopiaInicial(rutaRaizVersiones, ruta, pFecha);
                    }

                    mGestorArchivos.CrearFicheroFisico(ruta, pNombre + pExtension, pFichero);

                    mGestorArchivos.CrearFicheroFisico(rutaVersion, pNombre + pExtension, pFichero);

                    return Ok(true);
                }
                else
                {
                    return Ok(false);
                }
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return BadRequest(false);
            }
        }

        /// <summary>
        /// Sube un archivo ZIP.
        /// </summary>
        /// <param name="pFichero">Contenido del fichero</param>
        /// <param name="pNombre">Nombre del fichero</param>
        /// <param name="pExtension">Extensión del fichero</param>
        /// <param name="pProyectoID">Identificador de la comunidad</param>
        /// <param name="pFecha">Fecha de la subida</param>
        /// <param name="pNombreVersion">Nombre de la nueva versión</param>
        /// <returns></returns>
        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("AgregarZIP")]
        public IActionResult AgregarZIP(Byte[] pFichero, string pNombre, string pExtension, Guid pProyectoID, string pFecha, string pNombreVersion = null)
        {
            // Ruta archivos en uso
            string ruta;
            // Ruta raiz de historial
            string rutaRaizVersiones;
            // Ruta del historico que se esta subiendo
            string rutaVersion;
            if (string.IsNullOrEmpty(pNombreVersion))
            {
                ruta = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString());
                rutaRaizVersiones = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "historial");
                rutaVersion = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "historial", pFecha);
            }
            else
            {
                ruta = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion);
                rutaRaizVersiones = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion, "historial");
                rutaVersion = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion, "historial", pFecha);
            }
            try
            {
                if (pExtension.ToLower().Equals(".zip"))
                {
                    if (mGestorArchivos.ComprobarExisteDirectorio(ruta).Result)
                    {
                        CrearCopiaInicial(rutaRaizVersiones, ruta, pFecha);
                    }

                    mGestorArchivos.CrearFicheroFisico(rutaVersion, pNombre + pExtension, pFichero);

                    string rutaFichero = Path.Combine(mRutaImagenes, rutaVersion, pNombre + pExtension);
                    string rutaDescomprimir = Path.Combine(mRutaImagenes, ruta);

                    UtilZip.Descomprimir(rutaFichero, rutaDescomprimir);

                    return Ok(true);
                }
                else
                {
                    return Ok(false);
                }
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return BadRequest(false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pProyectoID"></param>
        /// <param name="pNombreVersion"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("DescargarZIP")]
        public IActionResult DescargarZIP(Guid pProyectoID, string pNombreVersion = null)
        {
            byte[] respuesta = null;
            // Ruta archivos en uso
            string ruta;

            if (string.IsNullOrEmpty(pNombreVersion))
            {
                ruta = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString());
            }
            else
            {
                ruta = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion);
            }
            try
            {
                string[] rutasArchivos = mGestorArchivos.ObtenerFicherosDeDirectorioYSubDirectorios(ruta).Result;

                using (var ms = new MemoryStream())
                {
                    using (var zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                    {
                        foreach (string archivo in rutasArchivos)
                        {
                            if (!archivo.StartsWith("versiones") && !archivo.StartsWith("historial") && !archivo.StartsWith("cms"))
                            {
                                byte[] bytes = System.IO.File.ReadAllBytes(Path.Combine(mRutaImagenes, ruta, archivo));
                                var entry = zipArchive.CreateEntry(archivo);
                                using (Stream s = entry.Open())
                                {
                                    s.Write(bytes, 0, bytes.Length);
                                    s.Flush();
                                }
                                entry = null;
                                bytes = null;
                            }
                        }
                    }
                    ms.Flush();

                    respuesta = ms.ToArray();
                }

                respuesta = System.IO.File.ReadAllBytes(Path.Combine(mRutaImagenes, ruta, "7317a29a-d846-4c54-9034-6a114c3658fe.rar"));

            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
            }

            return Ok(respuesta);
        }

        /// <summary>
        /// Obtiene el historial de subidas para la versión indicada.
        /// </summary>
        /// <param name="pProyectoID">Identificador de la comunidad</param>
        /// <param name="pNombreVersion">Nombre de la nueva versión</param>
        /// <returns></returns>
        [HttpGet]
        [Route("ObtenerHistorial")]
        public IActionResult ObtenerHistorial(Guid pProyectoID, string pNombreVersion)
        {
            try
            {
                string rutaRaizHistorial;
                if (string.IsNullOrEmpty(pNombreVersion))
                {
                    rutaRaizHistorial = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "historial");
                }
                else
                {
                    rutaRaizHistorial = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion, "historial");
                }

                if (mGestorArchivos.ComprobarExisteDirectorio(rutaRaizHistorial).Result)
                {
                    List<string[]> historial = new List<string[]>();

                    foreach (string directorio in mGestorArchivos.ObtenerSubdirectoriosDeDirectorio(rutaRaizHistorial).Result)
                    {
                        string rutaCompleta = Path.Combine(rutaRaizHistorial, directorio);

                        string[] archivos = mGestorArchivos.ObtenerFicherosDeDirectorio(rutaCompleta).Result;
                        string[] ficheros = new string[archivos.Length + 1];
                        ficheros[0] = directorio;
                        for (int i = 0; i < archivos.Length; i++)
                        {
                            string rutaArchivo = Path.Combine(rutaCompleta, archivos[i].ToLower());
                            ficheros[i + 1] = rutaArchivo.ToLower().Replace("\\", "/");
                        }
                        historial.Add(ficheros);
                    }
                    HistorialModel modeloHistorial = new HistorialModel();
                    modeloHistorial.Histroial = historial.OrderBy(version => version[0]).ToList();

                    XmlSerializer xsSubmit = new XmlSerializer(typeof(HistorialModel));
                    StringWriter sww = new StringWriter();
                    XmlWriter writer = XmlWriter.Create(sww);
                    xsSubmit.Serialize(writer, modeloHistorial);
                    string xml = sww.ToString();
                    sww.Close();
                    return Ok(xml);
                }
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
            return null;
        }

        /// <summary>
        /// Crea una carpeta para subir una nueva versión de estilos.
        /// </summary>
        /// <param name="pProyectoID">Identificador de la comunidad</param>
        /// <param name="pNombreVersion">Nombre de la nueva versión</param>
        /// <returns></returns>
        [HttpPost]
        [Route("CrearVersion")]
        public IActionResult CrearVersion(Guid pProyectoID, string pNombreVersion)
        {
            string rutaRaizVersion = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones", pNombreVersion);
            try
            {
                if (!mGestorArchivos.ComprobarExisteDirectorio(rutaRaizVersion).Result)
                {
                    mGestorArchivos.CrearDirectorioFisico(rutaRaizVersion);
                }
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return Ok(false);
            }
            return Ok(true);
        }

        /// <summary>
        /// Obtiene una lista de las versiones de estilos disponibles.
        /// </summary>
        /// <param name="pProyectoID">Identificador de la comunidad</param>
        /// <returns></returns>
        [HttpGet]
        [Route("ObtenerVersiones")]
        public IActionResult ObtenerVersiones(Guid pProyectoID)
        {
            try
            {
                string rutaRaizVersiones = Path.Combine("proyectos", "personalizacion", pProyectoID.ToString(), "versiones");

                if (mGestorArchivos.ComprobarExisteDirectorio(rutaRaizVersiones).Result)
                {
                    string[] directoriosVersiones = mGestorArchivos.ObtenerSubdirectoriosDeDirectorio(rutaRaizVersiones).Result;
                    return Ok(directoriosVersiones);
                }
                return null;
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pRuta"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("get-available-name")]
        public IActionResult ObtenerNombreDisponible(string relative_path)
        {
            string[] rutaYArchivo = mGestorArchivos.ObtenerDirectorioYArchivoDeNombreArchivo(relative_path);

            string rutaDirectorio = rutaYArchivo[0];
            string nombreArchivo = rutaYArchivo[1];

            try
            {
                FileInfo file = new FileInfo(nombreArchivo);
                bool existeArchivo = mGestorArchivos.ComprobarExisteArchivo(rutaDirectorio, nombreArchivo).Result;
                if (!existeArchivo)
                {
                    string nombre = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                    string extension = System.IO.Path.GetExtension(file.Name);
                    return Ok(nombre + extension);
                }
                else
                {
                    int v = 1;
                    string nombre = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                    string extension = System.IO.Path.GetExtension(file.Name);

                    string nuevoNombre = nombre + "_" + v + extension;
                    v++;
                    while (mGestorArchivos.ComprobarExisteArchivo(rutaDirectorio, nuevoNombre).Result)
                    {
                        nuevoNombre = nombre + "_" + v + extension;
                        v++;
                    }
                    return Content(nuevoNombre);
                }
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Agrega la imagen en miniatura a un documento.
        /// </summary>
        /// <param name="pFichero">Imagen</param>
        /// <param name="pNombre">Nombre de la imágen</param>
        /// <param name="pExtension">Extensión de la imágen</param>
        /// <returns>TRUE si todo ha ido bien, false en caso contario</returns>
        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("AgregarImagenEnMiniaturaADocumento")]
        public IActionResult AgregarImagenEnMiniaturaADocumento(GnossPersonImage pImagen)
        {
            byte[] pFichero = pImagen.file;
            string pNombre = pImagen.name;
            string pExtension = pImagen.extension;

            try
            {
                if (pExtension.ToLower().Equals(".png") || pExtension.ToLower().Equals(".jpg") || pExtension.ToLower().Equals(".jpeg") || pExtension.ToLower().Equals(".gif"))
                {
                    string documentoID = pNombre;

                    if (documentoID.Contains("_"))
                    {
                        documentoID = documentoID.Substring(0, documentoID.IndexOf("_"));
                    }

                    string ruta = Path.Combine("Documentos", "Miniatura", UtilArchivos.DirectorioDocumento(new Guid(documentoID)));

                    mGestorArchivos.CrearFicheroFisico(ruta, pNombre + pExtension, pFichero);

                    return Ok(true);
                }
                else
                {
                    return Ok(false);
                }
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return BadRequest(false);
            }
        }

        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("add-image-to-personal-document")]
        public IActionResult AgregarImagenDocumentoPersonal(GnossPersonImage pImagen)
        {
            try
            {
                pImagen.relative_path = Path.Combine("Documentos", "Personas", pImagen.person_id.ToString());
                EscribirImagen(pImagen);
                return Ok();
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("add-image-to-organization-document")]
        public IActionResult AgregarImagenDocumentoOrganizacion(OrganizationPersonImage pImagen)
        {
            try
            {
                pImagen.relative_path = Path.Combine("Documentos", "Organizaciones", pImagen.organization_id.ToString());
                EscribirImagen(pImagen);
                return Ok();
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("add-image-to-directory")]
        public IActionResult AgregarImagenADirectorio(GnossImage pImagen)
        {
            try
            {
                mLoggingService.AgregarEntrada($"Entra al método AgregarImagenADirectorio la imagen: \n\t - Nombre: {pImagen.name} \n\t - Ruta: {pImagen.relative_path}");
                string[] directorioYNombre = mGestorArchivos.ObtenerDirectorioYArchivoDeNombreArchivo(pImagen.name);

                if (!string.IsNullOrEmpty(directorioYNombre[0]))
                {
                    pImagen.relative_path = Path.Combine(pImagen.relative_path, directorioYNombre[0]);
                }

                pImagen.name = directorioYNombre[1];

                EscribirImagen(pImagen);
                return Ok();
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("add-image-to-ontology-directory")]
        public IActionResult AgregarImagenADirectorioOntologia(GnossImage pImagen)
        {
            try
            {
                EscribirImagen(pImagen, true);
                return Ok();
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        [HttpGet]
        [Route("get-image-from-ontology-directory")]
        public IActionResult ObtenerImagenDeDirectorioOntologia(string relative_path, string name, string extension)
        {
            try
            {
                return Content(ToBase64(mGestorArchivosOntologias.DescargarFicheroSinEncriptar(relative_path, name + extension).Result));
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }



        [HttpGet]
        [Route("get-image")]
        public IActionResult ObtenerImagen(string name, string extension)
        {
            try
            {
                string rutaImagen = Path.Combine(mRutaImagenes, name + extension);

                return Ok(ToBase64(mGestorArchivos.DescargarFichero("", name + extension).Result));
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Obtiene la imágen de un documento de tipo imágen.
        /// </summary>
        /// <param name="pNombre">Identificador del documento</param>
        /// <param name="pExtension">Extensión del documento</param>
        /// <param name="pPersonaID">Si el documento es de persona: Identificador de la persona a la que pertenece el documento; 
        /// sino Guid.Empty</param>
        /// <param name="pOrganizacionID">Si el documento es de organización: Identificador de la organización a la que pertenece
        /// el documento; sino Guid.Empty</param>
        /// <returns>Imagen</returns>
        /*[HttpGet]
        [Route("get-image-from-ontology-directory")]
        public IActionResult ObtenerImagenDocumento(string pNombre, string pExtension, Guid pPersonaID, Guid pOrganizacionID)
        {
            string rutaImagen = "Documentos";

            if (pPersonaID != Guid.Empty)
            {
                rutaImagen = Path.Combine(rutaImagen, "Personas", pPersonaID.ToString());
            }
            else if (pOrganizacionID != Guid.Empty)
            {
                rutaImagen = Path.Combine(rutaImagen, "Organizaciones", pOrganizacionID.ToString());
            }

            //return Ok(mGestorArchivos.DescargarFichero(rutaImagen, pNombre + pExtension));
            return Ok();
        }
        
        [HttpGet]
        [Route("get-image-ids-from-image-name")]
        public IActionResult ObtenerImagenDocumentoPersonal(string pNombre, string pExtension, Guid pPersonaID)
        {
            string rutaImagen = Path.Combine("Documentos", "Personas", pPersonaID.ToString());

            //return Ok(mGestorArchivos.DescargarFichero(rutaImagen, pNombre + pExtension));
            return Ok();
        }
        */
        [HttpGet]
        [Route("get-space-for-personal-document-image")]
        public IActionResult ObtenerEspacioImagenDocumentoPersonal(string name, string extension, Guid person_id)
        {
            try
            {
                string rutaImagen = Path.Combine("Documentos", "Personas", person_id.ToString());

                return Ok(ObtenerEspacionFichero(rutaImagen, name + extension));
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }


        [HttpGet]
        [Route("get-space-for-organization-document-image")]
        public IActionResult ObtenerEspacioImagenDocumentoOrganizacion(string name, string extension, Guid organization_id)
        {
            try
            {
                string rutaImagen = Path.Combine("Documentos", "Organizaciones", organization_id.ToString());

                return Ok(ObtenerEspacionFichero(rutaImagen, name + extension));
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("get-image-extension")]
        public IActionResult ObtenerExtensionImagen(string pRutaImagen, string pNombreImagen)
        {
            try
            {
                string rutaCompletaSinExtension = Path.Combine(pRutaImagen, pNombreImagen);
                List<string> formatosPermitidos = new List<string>() { ".png", ".jpg" };

                foreach (string extension in formatosPermitidos)
                {
                    string rutaCompleta = $"{rutaCompletaSinExtension}{extension}";

                    if (System.IO.File.Exists(rutaCompleta))
                    {
                        return Ok(extension);
                    }
                }

                return Ok("");
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al obtener la extension de la imagen. Parametros: pRutaImagen -> {pRutaImagen} ##### pNombreImagen -> {pNombreImagen}");
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("remove-category-images")]
        public IActionResult BorrarImagenesCategoria(Guid pCategoriaId, Guid pProyectoID)
        {
            try
            {
                string rutaImagenesCategoria = $"{UtilArchivos.ContentImagenesCategorias}/{pProyectoID.ToString().ToLower()}/{pCategoriaId.ToString().ToLower()}";

                if (!pCategoriaId.Equals(Guid.Empty) && !pProyectoID.Equals(Guid.Empty))
                {
                    mGestorArchivos.EliminarDirectorio(rutaImagenesCategoria);
                    return Ok();
                }
                else
                {
                    mLoggingService.GuardarLogError($"Error, el proyecto {pProyectoID} o la categoría {pCategoriaId} no son válidos");
                    return StatusCode(500);
                }
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al eliminar las imagenes de la categoría {pCategoriaId} del proyecto {pProyectoID}");
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("move-images-deleted-resource")]
        public async Task<IActionResult> MoverImagenesRecursoEliminadoOtroAlmacenamiento(string relative_path, Guid pDocumentoID, string pNombre)
        {
            string rutaTemporal = "";

            try
            {              
                if (Directory.Exists(relative_path))
                {
                    rutaTemporal = Path.Combine(GestionArchivos.ObtenerRutaFicherosDeRecursosTemporal(pDocumentoID), pNombre);
                    mGestorArchivos.MoverContenidoDirectorio(relative_path, rutaTemporal);

                    return Ok();
                }
                else
                {
                    mLoggingService.GuardarLogError($"El directorio {relative_path} no existe.");
                    return new EmptyResult();
                }
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al mover las imagenes del directorio '{relative_path}' a la ruta temporal '{rutaTemporal}'");
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("move-images-docs-videos-modified-resource")]
        public async Task<IActionResult> MoverImagenesRecursoModificadoOtroAlmacenamiento(Guid pDocumentoID)
        {
            try
            {
                string rutaTemporal = GestionArchivos.ObtenerRutaFicherosDeRecursosTemporal(pDocumentoID);
                if (!Directory.Exists(rutaTemporal))
                {
                    Directory.CreateDirectory(rutaTemporal);
                }
                List<string> ficherosRecurso = ObtenerFicherosRecurso(pDocumentoID);
                List<string> ficherosAEliminar = ObtenerFicherosAEliminarDeRecurso(pDocumentoID, ficherosRecurso);
                foreach (string file in ficherosAEliminar)
                {
                    string nombreFichero = file.Substring(file.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                    string ficheroTemp = Path.Combine(rutaTemporal, nombreFichero);
                    mGestorArchivos.MoverArchivo(file, ficheroTemp, true);
                }

                List<string> directoriosOpenSeaDragonEliminar = ObtenerDirectoriosOpenSeaDragonAEliminarDeRecursoModificado(pDocumentoID, ficherosRecurso);
                foreach (string carpeta in directoriosOpenSeaDragonEliminar)
                {
                    string carpetaTemp = Path.Combine(rutaTemporal, carpeta);
                    mGestorArchivos.MoverContenidoDirectorio(carpeta, carpetaTemp);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al mover las imagenes del recurso modificado '{pDocumentoID}'");
                return StatusCode(500);
            }
        }

        private List<string> ObtenerFicherosAEliminarDeRecurso(Guid pDocumentoID, List<string> pFicherosValidos)
        {
            List<string> ficherosRecursoEnServidor =
            [
                //Todas las imagenes, miniaturas, recortes.
                .. mGestorArchivos.ObtenerFicherosDeDirectorioRecurso(Path.Combine(UtilArchivos.ContentImagenes, UtilArchivos.ContentImagenesDocumentos, UtilArchivos.ContentImagenesSemanticas, UtilArchivos.DirectorioDocumento(pDocumentoID))),
                //Todos los archivos link
                .. mGestorArchivos.ObtenerFicherosDeDirectorioRecurso(Path.Combine(UtilArchivos.ContentDocLinks, UtilArchivos.DirectorioDocumento(pDocumentoID))),
                // Todos los videos
                .. mGestorArchivos.ObtenerFicherosDeDirectorioRecurso(Path.Combine(UtilArchivos.ContentVideosSemanticos, UtilArchivos.DirectorioDocumento(pDocumentoID))),
            ];

            List<string> ficherosEliminar = new List<string>();
            foreach (string file in ficherosRecursoEnServidor)
            {
                if (!mGestorArchivos.EsFicheroValido(file, pFicherosValidos))
                {
                    ficherosEliminar.Add(file);
                }
            }

            return ficherosEliminar;
        }

        private List<string> ObtenerDirectoriosOpenSeaDragonAEliminarDeRecursoModificado(Guid pDocumentoID, List<string> pFicherosValidos)
        {
            List<string> directoriosEnServidorDeRecurso = mGestorArchivos.ObtenerDirectoriosDeDirectorio(Path.Combine(UtilArchivos.ContentImagenesSemanticas, UtilArchivos.DirectorioDocumento(pDocumentoID)));

            List<string> foldersToDelete = new List<string>();
            foreach (string folder in directoriosEnServidorDeRecurso)
            {
                if (!mGestorArchivos.EsOpenSeaDragonValido(folder, pFicherosValidos))
                {
                    foldersToDelete.Add(folder);
                }
            }
            return foldersToDelete;
        }

        private List<string> ObtenerFicherosRecurso(Guid pDocumentoID)
        {
            string urlIntragnoss = _entityContext.ParametroAplicacion.Where(parametro => parametro.Parametro.Equals("UrlIntragnoss")).Select(item => item.Valor).FirstOrDefault();
            DocumentacionCN documentacionCN = new DocumentacionCN(_entityContext, mLoggingService, _configService, _servicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<DocumentacionCN>(), mLoggerFactory);
            Guid proyectoID = documentacionCN.ObtenerProyectoIDPorDocumentoID(pDocumentoID);
            FacetadoAD facetadoAD = new FacetadoAD(urlIntragnoss, mLoggingService, _entityContext, _configService, _virtuosoAD, _servicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<FacetadoAD>(), mLoggerFactory);
            List<string> listaFicheros = new List<string>();

            string consulta = $"{facetadoAD.NamespacesVirtuosoLectura} SELECT ?o WHERE {{ ?s ?p ?o. ?documento <http://gnoss/hasEntidad> ?s. FILTER (?documento = <{urlIntragnoss}{pDocumentoID.ToString().ToLower()}>) }}";
            FacetadoDS facetadoDS = new FacetadoDS();

            facetadoAD.LeerDeVirtuoso(consulta, "Archivos", facetadoDS, proyectoID.ToString());

            if (facetadoDS.Tables["Archivos"] != null)
            {
                foreach (DataRow fichero in facetadoDS.Tables["Archivos"].Rows)
                {
                    string triple = fichero[0].ToString();
                    if (triple.Contains(UtilArchivos.ContentImagenesSemanticas) || triple.Contains(UtilArchivos.ContentDocLinks) || triple.Contains(UtilArchivos.ContentDocumentosSem) || triple.Contains(UtilArchivos.ContentVideosSemanticos))
                    {
                        listaFicheros.Add(triple);
                    }
                }
            }

            return listaFicheros;
        }

        [HttpPost]
        [Route("move-image-modified-resource")]
        public async Task<IActionResult> MoverImagenRecursoModificadoOtroAlmacenamiento(string pImagen, Guid pDocumentoID)
        {
            try
            {
                string rutaDocumento = Path.Combine(mRutaImagenes, UtilArchivos.ContentImagenesDocumentos, UtilArchivos.ContentImagenesSemanticas, UtilArchivos.DirectorioDocumento(pDocumentoID));

                DirectoryInfo dirInfoRaiz = new DirectoryInfo(rutaDocumento);
                FileInfo[] ficheros = dirInfoRaiz.GetFiles();
                foreach (FileInfo fichero in ficheros)
                {
                    string rutaTemporal = Path.Combine(UtilArchivos.ContentTemporales, pImagen);
                    if (fichero.Exists && !fichero.FullName.Contains(pImagen))
                    {
                        mGestorArchivos.MoverArchivo(pImagen, rutaTemporal);
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al mover la imagen {pImagen} del recurso modificado '{pDocumentoID}'");
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("remove-images-from-resource")]
        public IActionResult BorrarImagenesDeRecurso(string relative_path)
        {
            try
            {
                if (!string.IsNullOrEmpty(relative_path) && mGestorArchivos.ComprobarExisteDirectorio(relative_path).Result)
                {
                    mGestorArchivos.EliminarDirectorioEnCascada(relative_path);

                    return Ok();
                }
                else
                {
                    mLoggingService.GuardarLogError($"El directorio {relative_path} no existe.");
                    return new EmptyResult();
                }
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Devuelve los nombres de las imágenes que contienen el NombreImagen.
        /// </summary>
        /// <param name="relative_path">Ruta del directorio</param>
        /// <param name="image_name">Nombre de la imagen usado para filtrar</param>
        /// <returns>Lista de cadenas con los nombres de las imágenes que contienen el nombre pasado.</returns>
        [HttpGet]
        [Route("get-image-ids-from-image-name")]
        public IActionResult ObtenerIDsImagenesPorNombreImagen(string relative_path, string image_name)
        {
            try
            {
                if (!string.IsNullOrEmpty(relative_path))
                {
                    if (mGestorArchivos.ComprobarExisteDirectorio(relative_path).Result)
                    {
                        return Ok(mGestorArchivos.ObtenerFicherosDeDirectorio(relative_path, image_name).Result.ToList());
                    }
                    else
                    {
                        return Ok();
                    }
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Devuelve la información relevante sobre los ficheros del directorio indicado
        /// </summary>
        /// <param name="relative_path">Nombre de la imagen usado para filtrar</param>
        /// <returns>Lista de cadenas con los nombres de las imágenes que contienen el nombre pasado.</returns>
        [HttpGet]
        [Route("get-files-data-from-directory")]
        public IActionResult ObtenerDatosFicherosDeCarpeta(string relative_path)
        {
            try
            {
                if (!string.IsNullOrEmpty(relative_path))
                {

                    if (mGestorArchivos.ComprobarExisteDirectorio(relative_path).Result)
                    {
                        return Ok(mGestorArchivos.ObtenerInformacionFicherosDeDirectorio(relative_path).Result.ToList());
                    }
                    else
                    {
                        return Ok();
                    }
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("remove-image-from-directory")]
        public IActionResult BorrarImagenDeDirectorio(string relative_image_path)
        {
            try
            {
                if (!string.IsNullOrEmpty(relative_image_path))
                {
                    string[] rutaYArchivo = mGestorArchivos.ObtenerDirectorioYArchivoDeNombreArchivo(relative_image_path);

                    string rutaDirectorio = rutaYArchivo[0];
                    string nombreArchivo = rutaYArchivo[1];

                    if (mGestorArchivos.ComprobarExisteArchivo(rutaDirectorio, nombreArchivo).Result)
                    {
                        mGestorArchivos.EliminarFicheroFisico(rutaDirectorio, nombreArchivo);
                    }
                }

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("remove-image")]
        public IActionResult BorrarImagen(string name)
        {
            try
            {
                BorrarImagenDeRuta(name);
                return Ok();
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("move-image-to-temp")]
        public IActionResult MoverImagenAlmacenamientoTemporal(string pRuta, Guid pDocumentoID)
        {
            try
            {
                FileInfo file = new FileInfo(pRuta);
                if (file.Exists)
                {
                    string rutaTemporal = GestionArchivos.ObtenerRutaFicherosDeRecursosTemporal(pDocumentoID);
                    if (!Directory.Exists(rutaTemporal))
                    {
                        Directory.CreateDirectory(rutaTemporal);
                    }
                    rutaTemporal = Path.Combine(rutaTemporal, file.Name);
                    mGestorArchivos.MoverArchivo(pRuta, rutaTemporal, true);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al mover la imagen '{pRuta}' del documento '{pDocumentoID}' al almacenamiento temporal");
                return StatusCode(500);
            }
        }

        [HttpDelete]
        [Route("remove-image-from-personal-document")]
        public IActionResult BorrarImagenDocumentoPersonal(string name, Guid person_id)
        {
            try
            {
                string rutaImagen = "";
                if (person_id != Guid.Empty)
                {
                    rutaImagen = Path.Combine("Personas", person_id.ToString());
                }

                BorrarImagenDeRuta(name, rutaImagen);

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("move-or-copy-image")]
        public IActionResult CopiarCortarImagen(CopyPasteImageModel pModelo)
        {
            string rutaOrigen = null;
            string rutaDestino = null;

            //Construimos la ruta de origen:
            if (pModelo.person_id_origin != Guid.Empty)
            {
                //string[] partesRuta = new string[] {pModelo.relative_path, "Personas", pModelo.person_id_origin.ToString() }
                rutaOrigen = Path.Combine(pModelo.relative_path, "Personas", pModelo.person_id_origin.ToString());
            }
            else if (pModelo.organization_id_origin != Guid.Empty)
            {
                string[] partesRuta = new string[] { pModelo.relative_path, "Organizaciones", pModelo.organization_id_origin.ToString() };
                rutaOrigen = Path.Combine(partesRuta);
            }
            else
            {
                string documentoOrigen = pModelo.document_id_origin.ToString();
                rutaOrigen = Path.Combine(pModelo.relative_path, documentoOrigen.Substring(0, 2), documentoOrigen.Substring(0, 4), documentoOrigen);
            }

            //Construimos la ruta de destino:
            if (pModelo.person_id_destination != Guid.Empty)
            {
                rutaDestino = Path.Combine(pModelo.relative_path, "Personas", pModelo.person_id_destination.ToString());
            }
            else if (pModelo.organization_id_destination != Guid.Empty)
            {
                string[] partesRuta = new string[] { pModelo.relative_path, "Organizaciones", pModelo.organization_id_destination.ToString() };
                rutaDestino = Path.Combine(partesRuta);
            }
            else
            {
                string documentoDestino = pModelo.document_id_destination.ToString();
                rutaDestino = Path.Combine(pModelo.relative_path, documentoDestino.Substring(0, 2), documentoDestino.Substring(0, 4), documentoDestino);
            }

            if (rutaOrigen != null && rutaDestino != null)
            {
                try
                {
                    if (!mGestorArchivos.ComprobarExisteDirectorio(rutaDestino).Result)
                    {
                        mGestorArchivos.CrearDirectorioFisico(rutaDestino);
                    }
                    mGestorArchivos.CopiarArchivo(rutaOrigen, rutaDestino, pModelo.document_id_origin.ToString(), pModelo.copy, pModelo.extension, pModelo.document_id_destination.ToString());

                    return Ok();
                }
                catch (Exception ex)
                {
                    mLoggingService.GuardarLogError(ex);
                    mLoggingService.GuardarLogError($"Error copiando el archivo de: \n\t{rutaOrigen} \n a {rutaDestino}");
                    return StatusCode(500);
                }
            }
            else
            {
                throw new Exception($"Ruta origen ({rutaOrigen}) y ruta destino ({rutaDestino}) deben ser no nulos: ");
            }
        }

        [HttpPost]
        [Route("copy-semantic-images")]
        public IActionResult CopiarImagenesSemanticas(Guid origin_document_id, Guid destination_document_id)
        {
            try
            {
                //Viejo directorio:
                string rutaImagen = Path.Combine(UtilArchivos.ContentImagenesDocumentos, "ImagenesSemanticas", origin_document_id.ToString());
                string rutaCopiaImagen = Path.Combine(UtilArchivos.ContentImagenesDocumentos, "ImagenesSemanticas", destination_document_id.ToString());

                mGestorArchivos.CopiarArchivosDeDirectorio(rutaImagen, rutaCopiaImagen);

                //Nuevo directorio:
                rutaImagen = Path.Combine(UtilArchivos.ContentImagenesDocumentos, UtilArchivos.ContentImagenesSemanticas, UtilArchivos.DirectorioDocumento(origin_document_id));
                rutaCopiaImagen = Path.Combine(UtilArchivos.ContentImagenesDocumentos, UtilArchivos.ContentImagenesSemanticas, UtilArchivos.DirectorioDocumento(destination_document_id));

                mGestorArchivos.CopiarArchivosDeDirectorio(rutaImagen, rutaCopiaImagen);

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Obtiene la imágen de un documento de tipo imágen.
        /// </summary>
        /// <param name="pNombre">Identificador del documento</param>
        /// <param name="pExtension">Extensión del documento</param>
        /// <param name="pPersonaID">Si el documento es de persona: Identificador de la persona a la que pertenece el documento; 
        /// sino Guid.Empty</param>
        /// <param name="pOrganizacionID">Si el documento es de organización: Identificador de la organización a la que pertenece
        /// el documento; sino Guid.Empty</param>
        /// <returns>Imagen</returns>
        [HttpGet]
        [Route("get-image-document")]
        public IActionResult ObtenerImagenDocumento(string pNombre, string pExtension, Guid pPersonaID, Guid pOrganizacionID)
        {
            string rutaImagen = "Documentos";

            if (pPersonaID != Guid.Empty)
            {
                rutaImagen = Path.Combine(rutaImagen, "Personas", pPersonaID.ToString());
            }
            else if (pOrganizacionID != Guid.Empty)
            {
                rutaImagen = Path.Combine(rutaImagen, "Organizaciones", pOrganizacionID.ToString());
            }
            var bytes = mGestorArchivos.DescargarFichero(GestionArchivos.TransformarRuta(rutaImagen), GestionArchivos.TransformarRuta(pNombre) + pExtension);
            return Content(ToBase64(bytes.Result));
        }
        #endregion

        #region Métodos

        /// <summary>
        /// Devuelve el espacio que ocupa una imagen.
        /// </summary>
        /// <param name="pRuta">Ruta del fichero a comprobar sin el nombre</param>
        /// <param name="pNombreFichero">Nombre del fichero a comprobar</param>
        /// <returns>Espacio que ocupa una imagen</returns>
        [NonAction]
        private double ObtenerEspacionFichero(string pRuta, string pNombreFichero)
        {
            long espacioImagen = mGestorArchivos.ObtenerTamanioArchivo(pRuta, pNombreFichero).Result;

            double espacioMB = 0;

            espacioMB = ((double)espacioImagen) / 1024 / 1024;

            return espacioMB;
        }

        [NonAction]
        private void EscribirImagen(GnossImage pImagen, bool pImagenDeOntologia = false)
        {
            try
            {
                string extension = pImagen.extension.ToLower();

                mLoggingService.AgregarEntrada($"Entra en ImagenesController.EscribirImagen con la imagen: \n\t -Nombre: {pImagen.name} \n\t -Ruta: {pImagen.relative_path} \n\t -Extensión: {extension}");

                if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".gif" || extension == ".webp")
                {
                    if (pImagenDeOntologia)
                    {
                        mGestorArchivosOntologias.CrearFicheroFisico(pImagen.relative_path, pImagen.name + pImagen.extension, pImagen.file);
                    }
                    else
                    {
                        mGestorArchivos.CrearFicheroFisico(pImagen.relative_path, pImagen.name + pImagen.extension, pImagen.file);
                    }
                }
                else
                {
                    throw new BadImageFormatException($"Los formatos permitidos son png, jpg, jpeg, gif y webp. La extensión recibida es {pImagen.extension}. Archivo {pImagen.name} y directorio {pImagen.relative_path}");
                }
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                throw;
            }
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
            //Si no existe el directorio de historial, creamos una copia del contenido original
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
                    mGestorArchivos.CopiarArchivosDeDirectorio(pRutaRaizHistorial, rutaVersionInicial, true);
                }
            }
        }

        [NonAction]
        private void BorrarImagenDeRuta(string pNombre, string pRutaImagen = "")
        {
            try
            {
                mGestorArchivos.EliminarFicheroFisico(pRutaImagen, pNombre);
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                throw;
            }
        }

        [NonAction]
        private string ToBase64(byte[] pBytes)
        {
            if (pBytes != null && pBytes.Length > 0)
            {
                return Convert.ToBase64String(pBytes);
            }
            return null;
        }

        #endregion

    }
}
