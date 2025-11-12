using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.CL.ServiciosGenerales;
using Es.Riam.Gnoss.FileManager;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.Web.Controles.ServicioImagenesWrapper.Model;
using Es.Riam.InterfacesOpenArchivos;
using Es.Riam.Util;
using Gnoss.Web.Intern.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Gnoss.Web.Intern.Controllers
{
    /// <summary>
    /// Descripción breve de ServicioDocumentosLink
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    [System.ComponentModel.ToolboxItem(false)]
    // Para permitir que se llame a este servicio Web desde un script, usando ASP.NET AJAX, quite la marca de comentario de la línea siguiente. 
    // [System.Web.Script.Services.ScriptService]
    public class DocumentosLinkController : ControllerBaseIntern
    {
        #region Miembros

        private static string mRutaDocumentos = null;

        public static string mAzureStorageConnectionString;

        private readonly GestionArchivos mGestorArchivos;

        private IHttpContextAccessor _httpContextAccessor;

        private IHostingEnvironment _env;
        private FileOperationsService _fileOperationsService;
        private IUtilArchivos _utilArchivos;
        private readonly new ILogger mLogger;
        #endregion

        #region Constructor

        public DocumentosLinkController(LoggingService loggingService, IHttpContextAccessor httpContextAccessor, IHostingEnvironment env, ConfigService configService, IUtilArchivos utilArchivos,RedisCacheWrapper redisCacheWrapper, ILoggerFactory loggerFactory):base(loggingService,redisCacheWrapper,configService, loggerFactory)
        {
            _httpContextAccessor = httpContextAccessor;
            _env = env;
            mLogger = mLoggerFactory.CreateLogger<DocumentosLinkController>();
            mRutaDocumentos = configService.GetRutaDoclinks();
            if (string.IsNullOrEmpty(mRutaDocumentos))
            {
                mRutaDocumentos = Path.Combine(env.ContentRootPath, UtilArchivos.ContentDocLinks);
            }
            _fileOperationsService = new FileOperationsService(mLoggingService, _env);

            mAzureStorageConnectionString = mConfigService.ObtenerAzureStorageConnectionString();

            if (!string.IsNullOrEmpty(mAzureStorageConnectionString))
            {
                mAzureStorageConnectionString += "/" + UtilArchivos.ContentDocLinks;
            }
            else if (mAzureStorageConnectionString == null)
            {
                mAzureStorageConnectionString = "";
            }
            _utilArchivos = utilArchivos;
            mGestorArchivos = new GestionArchivos(mLoggingService, utilArchivos, mLoggerFactory.CreateLogger<GestionArchivos>(), mLoggerFactory, pRutaArchivos: mRutaDocumentos, pAzureStorageConnectionString: mAzureStorageConnectionString);
        }

        #endregion

        #region Métodos web

        /// <summary>
        /// Agrega un documento a su directorio.
        /// </summary>
        /// <param name="pFichero">Fichero en Bytes</param>
        /// <param name="pDocumentoID">ID de documento</param>
        /// <param name="pNombre">Nombre del fichero</param>
        /// <param name="pExtension">Extensión del fichero</param>
        /// <returns>TRUE si ha ido bien, FALSE en caso contrario</returns>
        [HttpPost, Route("add-document")]
        public IActionResult AgregarDocumento(IFormFile pBytes, Guid pDocumentoID, string pNombre, string pExtension)
        {
            try
            {
                string directorio = UtilArchivos.DirectorioDocumento(pDocumentoID);
                mGestorArchivos.CrearFicheroFisicoDesdeStream(directorio, pNombre + pExtension, pBytes.OpenReadStream());
                return Ok(true);
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return BadRequest(false);
            }
        }

        /// <summary>
        /// Agrega un documento al directorio indicado
        /// </summary>
        /// <param name="pFile">Clase con el contenido del fichero, nombre, extensión y ruta a alojar</param>
        /// <returns></returns>
        [HttpPost]
        [Route("add-document-to-directory")]
        public IActionResult AgregarDocumentoADirectorio(GnossFile pFile)
        {
            try
            {
                if (string.IsNullOrEmpty(mAzureStorageConnectionString))
                {
                    mAzureStorageConnectionString = "";

                    if (pFile.path.StartsWith("imagenes/"))
                    {
                        pFile.path = pFile.path.Substring("imagenes/".Length);
                    }
                }
                mGestorArchivos.RutaFicheros = mConfigService.GetRutaImagenes();
                if (string.IsNullOrEmpty(mGestorArchivos.RutaFicheros))
                {
                    mGestorArchivos.RutaFicheros = Path.Combine(_env.ContentRootPath, UtilArchivos.ContentImagenes);
                }

                mGestorArchivos.CrearFicheroFisico(pFile.path, pFile.name + pFile.extension, pFile.file);

                return Ok("OK");
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return BadRequest("KO");
            }
        }

        [HttpDelete]
        [Route("remove-document-to-directory")]
        public IActionResult EliminarDocumentoDeDirectorio(GnossFile pFile)
        {
            try
            {
                if (string.IsNullOrEmpty(mAzureStorageConnectionString))
                {
                    mAzureStorageConnectionString = "";

                    if (pFile.path.StartsWith("imagenes/"))
                    {
                        pFile.path = pFile.path.Substring("imagenes/".Length);
                    }
                }
                mGestorArchivos.RutaFicheros = mConfigService.GetRutaImagenes();
                if (string.IsNullOrEmpty(mGestorArchivos.RutaFicheros))
                {
                    mGestorArchivos.RutaFicheros = Path.Combine(_env.ContentRootPath, UtilArchivos.ContentImagenes);
                }
                mGestorArchivos.EliminarFicheroFisico(pFile.path, pFile.name + pFile.extension);

                return Ok("OK");
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return BadRequest("KO");
            }
        }

        /// <summary>
        /// Borra el documento de su directorio
        /// </summary>
        /// <param name="pDocumentoID">identificador del documento</param>
        /// <param name="pNombreArchivo">nombre del archivo</param>
        /// <param name="pExtension">extension del archivo</param>
        /// <returns></returns>
        [HttpDelete]
        [Route("BorrarDocumentoDeDirectorio")]
        public IActionResult BorrarDocumentoDeDirectorio(Guid pDocumentoID, string pNombreArchivo, string pExtension)
        {
            try
            {
                string directorio = UtilArchivos.DirectorioDocumento(pDocumentoID);

                mGestorArchivos.EliminarFicheroFisico(directorio, pNombreArchivo + pExtension);

                return Ok(true);
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return BadRequest(false);
            }
        }

        /// <summary>
        /// Borra el directorio del documento
        /// </summary>
        /// <param name="pDocumentoID">identificador del documento</param>
        /// <returns>True si ha borrado correctamente. False en caso contrario</returns>
        [HttpGet]
        [Route("BorrarDirectorioDeDocumento")]
        public IActionResult BorrarDirectorioDeDocumento(Guid pDocumentoID)
        {
            try
            {
                string directorio = UtilArchivos.DirectorioDocumento(pDocumentoID);

                mGestorArchivos.EliminarDirectorioEnCascada(directorio);

                return Ok(true);
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return Ok(false);
            }
        }

        [HttpGet]
        [Route("move-doclinks-deleted-resource")]
        public async Task<IActionResult> MoverDocLinksRecursoEliminadoOtroAlmacenamiento(string relative_path, Guid pDocumentoID)
        {
            try
            {
                if (!string.IsNullOrEmpty(relative_path) && mGestorArchivos.ComprobarExisteDirectorio(relative_path).Result)
                {
                    string rutaTemporal = Path.Combine(GestionArchivos.ObtenerRutaFicherosDeRecursosTemporal(pDocumentoID), UtilArchivos.ContentDocLinks);
                    mGestorArchivos.MoverContenidoDirectorio(relative_path, rutaTemporal);
                    return Ok();
                }
                else
                {
                    mLoggingService.GuardarLog($"El directorio {relative_path} no existe.", mLogger);
                    return new EmptyResult();
                }
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al mover los documentos link del directorio '{relative_path}'", mLogger);
                return StatusCode(500);
            }
        }

        [HttpGet]
        [Route("move-doclink-modified-resource")]
        public async Task<IActionResult> MoverDocLinkRecursoModificadoOtroAlmacenamiento(string pDocLink, Guid pDocumentoID)
        {
            try
            {
                string rutaDocumento = Path.Combine(mRutaDocumentos, UtilArchivos.ContentDocLinks, UtilArchivos.DirectorioDocumento(pDocumentoID));
                DirectoryInfo dirInfoRaiz = new DirectoryInfo(rutaDocumento);
                FileInfo[] ficheros = dirInfoRaiz.GetFiles();

                foreach (FileInfo fichero in ficheros)
                {
                    string rutaTemporal = Path.Combine(UtilArchivos.ContentTemporales, pDocLink);
                    if (fichero.Exists && !fichero.FullName.Contains(pDocLink))
                    {
                        mGestorArchivos.MoverArchivo(pDocLink, rutaTemporal);
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al mover el documento link {pDocLink} del recurso modificado '{pDocumentoID}'", mLogger);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Copia los archivos de un documento a otro.
        /// </summary>
        /// <param name="pDocumentoIDOrigen">ID del documento de origen</param>
        /// <param name="pDocumentoIDDestino">ID del documento de destino</param>
        /// <returns>TRUE si ha ido bien, FALSE si no</returns>
        [HttpPut]
        [Route("CopiarDocumentoDeDirectorio")]
        public IActionResult CopiarArchivosDocumento(Guid pDocumentoIDOrigen, Guid pDocumentoIDDestino)
        {
            try
            {
                string directorioOrigen = UtilArchivos.DirectorioDocumento(pDocumentoIDOrigen);

                if (mGestorArchivos.ComprobarExisteDirectorio(directorioOrigen).Result)
                {
                    string directorioDestino = UtilArchivos.DirectorioDocumento(pDocumentoIDDestino);

                    mGestorArchivos.CopiarArchivosDeDirectorio(directorioOrigen, directorioDestino);
                }

                return Ok(true);
            }
            catch (Exception ex)
            {
                _fileOperationsService.GuardarLogError(ex);
                return BadRequest(false);
            }
        }

        #endregion

    }
}
