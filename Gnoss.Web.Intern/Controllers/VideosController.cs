using Es.Riam.Gnoss.CL.ServiciosGenerales;
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
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Gnoss.Web.Intern.Controllers
{
    /// <summary>
    /// Servicio web para gestionar los vídeos
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class VideosController : ControllerBase
    {
        #region Miembros

        /// <summary>
        /// Almacena la ruta de los videos.
        /// </summary>
        private static string mRutaVideos = null;
        /// <summary>
        /// Almacena la ruta del conversor de videos.
        /// </summary>
        private static string mRutaConversionVideos = null;
        /// <summary>
        /// Almacena la cadena de conexión de Azure.
        /// </summary>
        public static string mAzureStorageConnectionString;
        private LoggingService mLoggingService;
        private Conexion mConexion;
        private GestionArchivos mGestorArchivos;
        private ConfigService mConfigService;
        private ILogger mlogger;
        private ILoggerFactory mLoggerFactory;
        #endregion

        public VideosController(LoggingService loggingService, Conexion conexion, IHostingEnvironment env, IUtilArchivos utilArchivos, ConfigService configService, ILogger<VideosController> logger, ILoggerFactory loggerFactory)
        {
            mLoggingService = loggingService;
            mConexion = conexion;
            mConfigService = configService;
            mlogger = logger;
            mLoggerFactory = loggerFactory;
            mRutaVideos = configService.GetRutaVideos();
            if (string.IsNullOrEmpty(mRutaVideos))
            {
                mRutaVideos = Path.Combine(env.ContentRootPath, "Videos");
            }

            mRutaConversionVideos = mConfigService.GetRutaReproductorVideo();
            if (string.IsNullOrEmpty(mRutaConversionVideos))
            {
                mRutaConversionVideos = Path.Combine(env.ContentRootPath, "ReproductorVideo");
            }

            mAzureStorageConnectionString = mConfigService.ObtenerAzureStorageConnectionString();
            if (!string.IsNullOrEmpty(mAzureStorageConnectionString))
            {
                mAzureStorageConnectionString = $"{mAzureStorageConnectionString}/Videos";
            }
            else
            {
                mAzureStorageConnectionString = "";
            }

            mGestorArchivos = new GestionArchivos(loggingService, utilArchivos, mLoggerFactory.CreateLogger<GestionArchivos>(), mLoggerFactory, pRutaArchivos: mRutaVideos, pAzureStorageConnectionString: mAzureStorageConnectionString);
        }

        #region Métodos web

        /// <summary>
        /// Agrega un vídeo
        /// </summary>
        /// <param name="pFichero">Vídeo a añadir</param>
        /// <param name="pExtension">Extensión del vídeo</param>
        /// <param name="pDocumentoID">Identificador del recurso</param>
        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("AgregarVideo")]
        public IActionResult AgregarVideo(IFormFile pFichero, string pExtension, Guid pDocumentoID)
        {
            try
            {
                return Ok(AgregarVideoPersonal(pFichero, pExtension, pDocumentoID, Guid.Empty));
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al agregar el vídeo {pDocumentoID}{pExtension}");
                return StatusCode(500);
            }            
        }

        /// <summary>
        /// Agrega un vídeo personal
        /// </summary>
        /// <param name="pFichero">Vídeo a añadir</param>
        /// <param name="pExtension">Extensión del vídeo</param>
        /// <param name="pDocumentoID">Identificador del recurso</param>
        /// <param name="pPersonaID">Identificador de la persona</param>
        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("AgregarVideoPersonal")]
        public int AgregarVideoPersonal(IFormFile pFichero, string pExtension, Guid pDocumentoID, Guid pPersonaID)
        {
            try
            {
                bool resultado = CrearFichero(pFichero, $"{pDocumentoID}{pExtension}", pPersonaID, 0);
                if (resultado)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al agregar el vídeo al espacio personal. Datos petición: Fichero -> {pDocumentoID}{pExtension} ||| pPersonaID -> {pPersonaID} ||| pFichero tiene valor? -> {pFichero != null}");
                return 0;
            }
        }

        /// <summary>
        /// Añade un vídeo a una organización
        /// </summary>
        /// <param name="pFichero">Vídeo a añadir</param>
        /// <param name="pExtension">Extensión del vídeo</param>
        /// <param name="pDocumentoID">Identificador del recurso</param>
        /// <param name="pOrganizacionID">Identificador de la organización</param>
        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("AgregarVideoOrganizacion")]
        public int AgregarVideoOrganizacion(IFormFile pFichero, string pExtension, Guid pDocumentoID, Guid pOrganizacionID)
        {
            try
            {
                bool resultado = CrearFichero(pFichero, $"{pDocumentoID}{pExtension}", pOrganizacionID, 1);
                if (resultado)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al agregar el vídeo de organización. Datos petición: Fichero -> {pDocumentoID}{pExtension} ||| pOrganizacion -> {pOrganizacionID} ||| pFichero tiene valor? -> {pFichero != null}");
                return 0;
            }
        }

        /// <summary>
        /// Añade un vídeo a un recurso semántico
        /// </summary>
        /// <param name="pFichero">Bytes del vídeo</param>
        /// <param name="pExtension">Extensión del vídeo</param>
        /// <param name="pDocumentoID">Identificador del recurso semántico</param>
        /// <param name="pVideoID">Identificador del vídeo</param>
        [HttpPost]
        [DisableRequestSizeLimit, RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [Route("AgregarVideoSemantico")]
        public int AgregarVideoSemantico(IFormFile pFichero, string pExtension, Guid pDocumentoID, Guid pVideoID)
        {
            try
            {
                bool resultado = CrearFichero(pFichero, $"{pVideoID}.flv", pDocumentoID, 2);
                if (resultado)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al agregar el vídeo semántico. Datos petición: Fichero -> {pDocumentoID}{pExtension} ||| pVideoID -> {pVideoID} ||| pFichero tiene valor? -> {pFichero != null}");
                return 0;
            }
        }

        /// <summary>
        /// Obtiene el espacio que ocupa un vídeo personal
        /// </summary>
        /// <param name="pDocumentoID">Identificador del vídeo</param>
        /// <param name="pPersonaID">Identificador de la persona</param>
        [HttpGet]
        [Route("ObtenerEspacioVideoPersonal")]
        public IActionResult ObtenerEspacioVideoPersonal(Guid pDocumentoID, Guid pPersonaID)
        {
            try
            {
                string rutaFichero = Path.Combine(mRutaVideos, "VideosPersonales", pPersonaID.ToString(), $"{pDocumentoID}.flv");
                return Ok(ObtenerEspacionFichero(rutaFichero));
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al obtener el vídeo {pDocumentoID} de la persona {pPersonaID}");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Elimina los videos de un recurso
        /// </summary>
        /// <param name="pRuta">Directorio donde se encuentran los videos que hay que eliminar</param>
        /// <returns></returns>
        [HttpGet]
        [Route("BorrarVideosDeRecurso")]
        public int BorrarVideosDeRecurso(string pRuta)
        {
            try
            {
                if (!string.IsNullOrEmpty(pRuta) && mGestorArchivos.ComprobarExisteDirectorio(pRuta).Result)
                {
                    mGestorArchivos.EliminarDirectorioEnCascada(pRuta);
                }

                return 1;
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al eliminar los videos de la ruta: '{pRuta}'");
                return 0;
            }
        }

        [HttpGet]
        [Route("move-videos-deleted-resource")]
        public async Task<IActionResult> MoverVideosRecursoEliminadoOtroAlmacenamiento(string relative_path, Guid pDocumentoID)
        {
            try
            {
                if (!string.IsNullOrEmpty(relative_path) && mGestorArchivos.ComprobarExisteDirectorio(relative_path).Result)
                {
                    string rutaTemporal = Path.Combine(GestionArchivos.ObtenerRutaFicherosDeRecursosTemporal(pDocumentoID), UtilArchivos.ContentVideosSemanticos);
                    mGestorArchivos.MoverContenidoDirectorio(relative_path, rutaTemporal);

                    return Ok();
                }
                else
                {
                    mLoggingService.GuardarLog($"El directorio {relative_path} no existe.");
                    return new EmptyResult();
                }
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al mover los videos del directorio '{relative_path}'");
                return StatusCode(500);
            }
        }

        [HttpGet]
        [Route("move-video-modified-resource")]
        public async Task<IActionResult> MoverVideoRecursoModificadoOtroAlmacenamiento(string pVideo, Guid pDocumentoID)
        {
            try
            {
                string rutaDocumento = Path.Combine(mRutaVideos, UtilArchivos.ContentVideosSemanticos, UtilArchivos.DirectorioDocumento(pDocumentoID));
                DirectoryInfo dirInfoRaiz = new DirectoryInfo(rutaDocumento);
                FileInfo[] ficheros = dirInfoRaiz.GetFiles();

                foreach (FileInfo fichero in ficheros)
                {
                    string rutaTemporal = Path.Combine(UtilArchivos.ContentTemporales, pVideo);
                    if (fichero.Exists && !fichero.FullName.Contains(pVideo))
                    {
                        mGestorArchivos.MoverArchivo(pVideo, rutaTemporal);
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al mover el video {pVideo} del recurso modificado '{pDocumentoID}'");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Obtiene el espacio que ocupa un vídeo de organización
        /// </summary>
        /// <param name="pDocumentoID">Identificador del vídeo</param>
        /// <param name="pOrganizacionID">Identificador de la organización</param>
        [HttpGet]
        [Route("ObtenerEspacioVideoOrganizacion")]
        public IActionResult ObtenerEspacioVideoOrganizacion(Guid pDocumentoID, Guid pOrganizacionID)
        {
            try
            {
                string rutaFichero = Path.Combine(mRutaVideos, "VideosOrganizaciones", pOrganizacionID.ToString(), $"{pDocumentoID}.flv");
                return Ok(ObtenerEspacionFichero(rutaFichero));
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al obtener el espacio del vídeo {pDocumentoID} de la organización {pOrganizacionID}");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Elimina un vídeo
        /// </summary>
        /// <param name="pDocumentoID">Identificador del vídeo que vamos a borrar</param>
        [HttpDelete]
        [Route("BorrarVideo")]
        public IActionResult BorrarVideo(Guid pDocumentoID)
        {
            try
            {
                return Ok(BorrarVideoPersonal(pDocumentoID, Guid.Empty));
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al borrar el vídeo del documento {pDocumentoID}");
                return StatusCode(500);
            }       
        }

        /// <summary>
        /// Elimina un vídeo en específico de una persona
        /// </summary>
        /// <param name="pDocumentoID">Identificador del vídeo</param>
        /// <param name="pPersonaID">Identificador de la persona de la que se quiere eliminar un vídeo</param>
        [HttpDelete]
        [Route("BorrarVideoPersonal")]
        public bool BorrarVideoPersonal(Guid pDocumentoID, Guid pPersonaID)
        {
            try
            {
                return BorrarVideo(pDocumentoID, pPersonaID, 0);
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al borrar el vídeo {pDocumentoID} de la persona {pPersonaID}");
                return false;
            }            
        }

        /// <summary>
        /// Elimina un vídeo de una organización
        /// </summary>
        /// <param name="pDocumentoID">Identificador del vídeo</param>
        /// <param name="pOrganizacionID">Identificador de la organización de la que se quiere eliminar un vídeo</param>
        [HttpDelete]
        [Route("BorrarVideoOrganizacion")]
        public bool BorrarVideoOrganizacion(Guid pDocumentoID, Guid pOrganizacionID)
        {
            try
            {
                return BorrarVideo(pDocumentoID, pOrganizacionID, 1);
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al borrar el vídeo {pDocumentoID} de la organización {pOrganizacionID}");
                return false;
            }            
        }

        /// <summary>
        /// Elimina un vídeo de un recurso semántico
        /// </summary>
        /// <param name="pDocumentoID">Identificador del recurso semántico que tiene el vídeo</param>
        /// <param name="pVideoID">Identificador del vídeo</param>
        [HttpDelete]
        [Route("BorrarVideoSemantico")]
        public bool BorrarVideoSemantico(Guid pDocumentoID, Guid pVideoID)
        {
            try
            {
                return BorrarVideo(pVideoID, pDocumentoID, 2);
            }
            catch(Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al borrar el vídeo semántico {pVideoID} del documento {pDocumentoID}");
                return false;
            }            
        }

        /// <summary>
        /// Copia el vídeo de un recurso a otro
        /// </summary>
        /// <param name="pDocumentoID">Identificador del recurso original que tiene el vídeo</param>
        /// <param name="pDocumentoIDCopia">Identificador del recurso al que se va a copiar el vídeo</param>
        /// <param name="pPersonaID">Identificador de la persona original</param>
        /// <param name="pOrganizacionID">Identificador de la organización original</param>
        /// <param name="pPersonaIDDestino">Identificador de la persona a la que se va a copiar el vídeo</param>
        /// <param name="pOrganizacionIDDestino">Identificador de la organización en la que se va a copiar el vídeo</param>
        [HttpPost]
        [Route("CopiarVideo")]
        public IActionResult CopiarVideo(Guid pDocumentoID, Guid pDocumentoIDCopia, Guid pPersonaID, Guid pOrganizacionID, Guid pPersonaIDDestino, Guid pOrganizacionIDDestino)
        {
            string rutaVideo = "";
            string rutaCopiaVideo = "";
            try
            {                
                if (pPersonaID != Guid.Empty)
                {
                    rutaVideo = Path.Combine(mRutaVideos, "VideosPersonales",  pPersonaID.ToString(), $"{pDocumentoID}.flv");
                }
                else if (pOrganizacionID != Guid.Empty)
                {
                    rutaVideo = Path.Combine(mRutaVideos, "VideosOrganizaciones",  pOrganizacionID.ToString(), $"{pDocumentoID}.flv");
                }
                else
                {
                    rutaVideo = Path.Combine(mRutaVideos, $"{pDocumentoID}.flv");
                }

                if (pPersonaIDDestino != Guid.Empty)
                {
                    rutaCopiaVideo = Path.Combine(mRutaVideos, "VideosPersonales",  pPersonaIDDestino.ToString(), $"{pDocumentoIDCopia}.flv");
                }
                else if (pOrganizacionIDDestino != Guid.Empty)
                {
                    rutaCopiaVideo = Path.Combine(mRutaVideos, "VideosOrganizaciones",  pOrganizacionIDDestino.ToString(), $"{pDocumentoIDCopia}.flv");
                }
                else
                {
                    rutaCopiaVideo = Path.Combine(mRutaVideos, $"{pDocumentoIDCopia}.flv");
                }

                FileInfo fich = new FileInfo(rutaVideo);
                fich.CopyTo(rutaCopiaVideo);

                return Ok(true);
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Ha habido un error al copiar el vídeo {pDocumentoID} al DocumentoCopia {pDocumentoIDCopia}. Persona -> {pPersonaID} ||| PersonaDestino -> {pPersonaIDDestino} ||| Organización -> {pOrganizacionID} ||| OrganizaciónDestino -> {pOrganizacionIDDestino} ||| RutaVideo: {rutaVideo} ||| RutaVideoCopia -> {rutaCopiaVideo}");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Copia el vídeo de un recurso semántico a otro recurso
        /// </summary>
        /// <param name="pDocumentoID">Identificador del recurso con el vídeo original</param>
        /// <param name="pDocumentoIDCopia">Identificador del recurso al que se va a copiar el vídeo</param>
        /// <param name="pVideoID">Identificador que representa el vídeo a copiar</param>
        [HttpPost]
        [Route("CopiarVideoSemantico")]
        public IActionResult CopiarVideoSemantico(Guid pDocumentoID, Guid pDocumentoIDCopia, Guid pVideoID)
        {
            try
            {
                string rutaVideo = Path.Combine(mRutaVideos, "VideosSemanticos", UtilArchivos.DirectorioDocumento(pDocumentoID), $"{pVideoID}.flv");
                string rutaCopiaVideo = Path.Combine(mRutaVideos, "VideosSemanticos", UtilArchivos.DirectorioDocumento(pDocumentoID), $"{pVideoID}.flv");
                DirectoryInfo directorioCopia = new DirectoryInfo(Path.Combine(mRutaVideos, "VideosSemanticos", UtilArchivos.DirectorioDocumento(pDocumentoID)));
                
                if (!directorioCopia.Exists)
                {
                    directorioCopia.Create();
                }

                FileInfo fich = new FileInfo(rutaVideo);
                fich.CopyTo(rutaCopiaVideo);

                return Ok(true);
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Ha habido un error al copiar el vídeo semántico {pVideoID} del Documento {pDocumentoID} al documento {pDocumentoIDCopia}.");
                return StatusCode(500);
            }
        }

        #endregion

        #region Métodos

        /// <summary>
        /// Crea un fichero a partir de la ruta y el array de bytes pasados como parámetros
        /// </summary>
        /// <param name="pBytes">Bytes del fichero</param>
        /// <param name="pNuevoNombre">Nombre del video</param>
        /// <param name="pCarpetaID">Identificador de persona o organización</param>
        /// <param name="pTipoVideo">Indica si el video es personal, de una organización o semántico</param>
        [NonAction]
        private bool CrearFichero(IFormFile pBytes, string pNuevoNombre, Guid pCarpetaID, int pTipoVideo)
        {
            FileStream ficheroAGuardar = null;
            try
            {
                string rutaFichero;
                DirectoryInfo directorio;

                if (pCarpetaID == Guid.Empty)
                {
                    rutaFichero = Path.Combine(mRutaVideos, pNuevoNombre);
                    directorio = new DirectoryInfo(mRutaVideos);
                }
                else if (pTipoVideo == 0)
                {
                    rutaFichero = Path.Combine(mRutaVideos, "VideosPersonales", UtilArchivos.DirectorioDocumento(pCarpetaID), pNuevoNombre);
                    directorio = new DirectoryInfo(Path.Combine(mRutaVideos, "VideosPersonales", UtilArchivos.DirectorioDocumento(pCarpetaID)));
                }
                else if (pTipoVideo == 1)
                {
                    rutaFichero = Path.Combine(mRutaVideos, "VideosOrganizaciones", UtilArchivos.DirectorioDocumento(pCarpetaID), pNuevoNombre);
                    directorio = new DirectoryInfo(Path.Combine(mRutaVideos, "VideosOrganizaciones", UtilArchivos.DirectorioDocumento(pCarpetaID)));
                }
                else
                {
                    rutaFichero = Path.Combine(mRutaVideos, "VideosSemanticos", UtilArchivos.DirectorioDocumento(pCarpetaID), pNuevoNombre);
                    directorio = new DirectoryInfo(Path.Combine(mRutaVideos, "VideosSemanticos", UtilArchivos.DirectorioDocumento(pCarpetaID)));
                }

                if (!directorio.Exists)
                {
                    directorio.Create();
                }

                //Borro el fichero antiguo:
                FileInfo ficheroAntiguo = new FileInfo(rutaFichero);
                if (ficheroAntiguo.Exists)
                {
                    ficheroAntiguo.Delete();
                }
                ficheroAntiguo = null;
                Stream stream = pBytes.OpenReadStream();
                byte[] buffer = new byte[1048576];
                stream.Seek(0, SeekOrigin.Begin);
                int num = 0;
                ficheroAGuardar = new FileStream(rutaFichero, FileMode.Create, FileAccess.Write);
                while ((num = stream.Read(buffer, 0, 1048576)) > 0)
                {
                    ficheroAGuardar.Write(buffer, 0, num);
                    ficheroAGuardar.Flush();
                }
                ficheroAGuardar.Close();
                ficheroAGuardar.Dispose();
                ficheroAGuardar = null;
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"No se ha podido crear el fichero");
                throw;
            }
            finally
            {
                if (ficheroAGuardar != null)
                {
                    ficheroAGuardar.Close();
                    ficheroAGuardar.Dispose();
                    ficheroAGuardar = null;
                }
            }

            return true;
        }

        /// <summary>
        /// Devuelve el espacio que ocupa un video.
        /// </summary>
        /// <param name="pRuta">Ruta de el video</param>
        /// <returns>Espacio que ocupa el video</returns>
        [NonAction]
        private double ObtenerEspacionFichero(string pRuta)
        {
            double tamanoVideo = 0;
            try
            {
                FileInfo video = new FileInfo(pRuta);
                
                if (video.Exists)
                {
                    tamanoVideo = ((double)video.Length) / 1024 / 1024;
                }

                return tamanoVideo;
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Ha habido un error al obtener el peso del fichero de la ruta: {pRuta}");
                return tamanoVideo;
            }          
        }

        /// <summary>
        /// Borra un video.
        /// </summary>
        /// <param name="pDocumentoID">Identificador de documento</param>
        /// <param name="pCarpetaID">Identificador de persona o organización</param>
        /// <param name="pTipoVideo">Indica si el video es de una organización, de una persona o semantico</param>
        /// <returns>True si el video es borrado, false en caso contario</returns>
        [NonAction]
        private bool BorrarVideo(Guid pDocumentoID, Guid pCarpetaID, int pTipoVideo)
        {
            string rutaVideo = "";
            try
            {               
                if (pCarpetaID == Guid.Empty)
                {
                    rutaVideo = Path.Combine(mRutaVideos, $"{pDocumentoID}.flv");
                }
                else if (pTipoVideo == 0)
                {
                    rutaVideo = Path.Combine(mRutaVideos, "VideosPersonales", UtilArchivos.DirectorioDocumento(pCarpetaID), $"{pDocumentoID}.flv");
                }
                else if (pTipoVideo == 1)
                {
                    rutaVideo = Path.Combine(mRutaVideos, "VideosOrganizaciones", UtilArchivos.DirectorioDocumento(pCarpetaID), $"{pDocumentoID}.flv");
                }
                else
                {
                    rutaVideo = Path.Combine(mRutaVideos, "VideosSemanticos", UtilArchivos.DirectorioDocumento(pCarpetaID), $"{pDocumentoID}.flv");
                }

                //Borramos el fichero físico.
                FileInfo fich = new FileInfo(rutaVideo);
                fich.Delete();

                return true;
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Ha habido un error al borrar el vídeo de la ruta: {rutaVideo}");
                return false;
            }
        }

        #endregion
    }
}