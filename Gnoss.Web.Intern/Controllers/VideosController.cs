using Es.Riam.Gnoss.FileManager;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.InterfacesOpenArchivos;
using Gnoss.Web.Intern.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;

namespace Gnoss.Web.Intern.Controllers
{
    /// <summary>
    /// Descripción breve de ServicioVideos
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

        public static string mAzureStorageConnectionString;

        private GestionArchivos mGestorArchivos;

        private LoggingService mLoggingService;

        private Conexion mConexion;

        private FileOperationsService _fileOperationsService;
        private IUtilArchivos _utilArchivos;

        #endregion

        public VideosController(LoggingService loggingService, Conexion conexion, IHostingEnvironment env, IUtilArchivos utilArchivos)
        {
            mLoggingService = loggingService;
            mConexion = conexion;
            
            mRutaVideos = Path.Combine(env.ContentRootPath, "Videos");
            mRutaConversionVideos = Path.Combine(env.ContentRootPath, "ReproductorVideo");
            string rutaConfigs = Path.Combine(env.ContentRootPath, "config");

            //mRutaVideos = this.Server.MapPath("~/" + "Videos");
            //mRutaConversionVideos = this.Server.MapPath("~/" + "ReproductorVideo");
            //string rutaConfigs = this.Server.MapPath("/config");

            mAzureStorageConnectionString = mConexion.ObtenerParametro("Config/gnoss.config", "config/AzureStorageConnectionString", false);
            if (!string.IsNullOrEmpty(mAzureStorageConnectionString))
            {
                mAzureStorageConnectionString += "/Videos";
            }
            else
            {
                mAzureStorageConnectionString = "";
            }
            _utilArchivos = utilArchivos;
            mGestorArchivos = new GestionArchivos(mLoggingService, utilArchivos, pRutaArchivos: mRutaVideos, pAzureStorageConnectionString: mAzureStorageConnectionString);
            _fileOperationsService = new FileOperationsService(mLoggingService, env);
        }

        #region Métodos web

        [HttpPost]
        [Route("AgregarVideo")]
        public IActionResult AgregarVideo(IFormFile pFichero, string pExtension, Guid pDocumentoID)
        {
            return Ok(AgregarVideoPersonal(pFichero, pExtension, pDocumentoID, Guid.Empty));
        }

        [HttpPost]
        [Route("AgregarVideoPersonal")]
        public int AgregarVideoPersonal(IFormFile pFichero, string pExtension, Guid pDocumentoID, Guid pPersonaID)
        {
            try
            {
                bool resultado = CrearFichero(pFichero, $"{pDocumentoID}{pExtension}", pPersonaID, 0);
                if (resultado)
                {
                    return 1;//Ha ido bien
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                return 0;//Algo falló
            }
        }

        [HttpPost]
        [Route("AgregarVideoOrganizacion")]
        public int AgregarVideoOrganizacion(IFormFile pFichero, string pExtension, Guid pDocumentoID, Guid pOrganizacionID)
        {
            try
            {
                bool resultado = CrearFichero(pFichero, $"{pDocumentoID}{pExtension}", pOrganizacionID, 1);
                if (resultado)
                {
                    return 1;//Ha ido bien
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                return 0;//Algo falló
            }
        }

        [HttpPost]
        [Route("AgregarVideoSemantico")]
        public int AgregarVideoSemantico(IFormFile pFichero, string pExtension, Guid pDocumentoID, Guid pVideoID)
        {
            try
            {
                bool resultado = CrearFichero(pFichero, $"{pVideoID}{pExtension}", pDocumentoID, 2);
                if (resultado)
                {
                    return 1;//Ha ido bien
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                return 0;//Algo falló
            }
        }

        [HttpGet]
        [Route("ObtenerEspacioVideoPersonal")]
        public IActionResult ObtenerEspacioVideoPersonal(Guid pDocumentoID, Guid pPersonaID)
        {
            string rutaFichero = Path.Combine(mRutaVideos, "VideosPersonales", pPersonaID.ToString(), pDocumentoID.ToString() + ".flv");
            return Ok(ObtenerEspacionFichero(rutaFichero));
        }

        [HttpGet]
        [Route("ObtenerEspacioVideoOrganizacion")]
        public IActionResult ObtenerEspacioVideoOrganizacion(Guid pDocumentoID, Guid pOrganizacionID)
        {
            string rutaFichero = Path.Combine(mRutaVideos, "VideosOrganizaciones",  pOrganizacionID.ToString(), pDocumentoID.ToString() + ".flv");
            return Ok(ObtenerEspacionFichero(rutaFichero));
        }

        [HttpDelete]
        [Route("BorrarVideo")]
        public IActionResult BorrarVideo(Guid pDocumentoID)
        {
            return Ok(BorrarVideoPersonal(pDocumentoID, Guid.Empty));
        }

        [HttpDelete]
        [Route("BorrarVideoPersonal")]
        public bool BorrarVideoPersonal(Guid pDocumentoID, Guid pPersonaID)
        {
            return BorrarVideo(pDocumentoID, pPersonaID, 0);
        }

        [HttpDelete]
        [Route("BorrarVideoOrganizacion")]
        public bool BorrarVideoOrganizacion(Guid pDocumentoID, Guid pOrganizacionID)
        {
            return BorrarVideo(pDocumentoID, pOrganizacionID, 1);
        }

        [HttpDelete]
        [Route("BorrarVideoSemantico")]
        public bool BorrarVideoSemantico(Guid pDocumentoID, Guid pVideoID)
        {
            return BorrarVideo(pVideoID, pDocumentoID, 2);
        }


        [HttpPost]
        [Route("CopiarVideo")]
        public IActionResult CopiarVideo(Guid pDocumentoID, Guid pDocumentoIDCopia, Guid pPersonaID, Guid pOrganizacionID, Guid pPersonaIDDestino, Guid pOrganizacionIDDestino)
        {
            try
            {
                string rutaVideo;
                string rutaCopiaVideo;
                if (pPersonaID != Guid.Empty)
                {
                    rutaVideo = Path.Combine(mRutaVideos, "VideosPersonales",  pPersonaID.ToString(), pDocumentoID.ToString() + ".flv");
                }
                else if (pOrganizacionID != Guid.Empty)
                {
                    rutaVideo = Path.Combine(mRutaVideos, "VideosOrganizaciones",  pOrganizacionID.ToString(), pDocumentoID.ToString() + ".flv");
                }
                else
                {
                    rutaVideo = Path.Combine(mRutaVideos, pDocumentoID.ToString() + ".flv");
                }


                if (pPersonaIDDestino != Guid.Empty)
                {
                    rutaCopiaVideo = Path.Combine(mRutaVideos, "VideosPersonales",  pPersonaIDDestino.ToString(), pDocumentoIDCopia.ToString() + ".flv");
                }
                else if (pOrganizacionIDDestino != Guid.Empty)
                {
                    rutaCopiaVideo = Path.Combine(mRutaVideos, "VideosOrganizaciones",  pOrganizacionIDDestino.ToString(), pDocumentoIDCopia.ToString() + ".flv");
                }
                else
                {
                    rutaCopiaVideo = Path.Combine(mRutaVideos, pDocumentoIDCopia.ToString() + ".flv");
                }

                FileInfo fich = new FileInfo(rutaVideo);
                fich.CopyTo(rutaCopiaVideo);

                return Ok(true);
            }
            catch (Exception)
            {
                return BadRequest(false);
            }
        }

        [HttpPost]
        [Route("CopiarVideoSemantico")]
        public IActionResult CopiarVideoSemantico(Guid pDocumentoID, Guid pDocumentoIDCopia, Guid pVideoID)
        {
            try
            {
                string rutaVideo = Path.Combine(mRutaVideos, "VideosSemanticos",  pDocumentoID.ToString(), pVideoID.ToString() + ".flv");
                string rutaCopiaVideo = Path.Combine(mRutaVideos, "VideosSemanticos",  pDocumentoIDCopia.ToString(), pVideoID.ToString() + ".flv");

                DirectoryInfo directorioCopia = new DirectoryInfo(Path.Combine(mRutaVideos, "VideosSemanticos",  pDocumentoIDCopia.ToString()));
                if (!directorioCopia.Exists)
                {
                    directorioCopia.Create();
                }

                FileInfo fich = new FileInfo(rutaVideo);
                fich.CopyTo(rutaCopiaVideo);

                return Ok(true);
            }
            catch (Exception)
            {
                return BadRequest(false);
            }
        }

        #endregion

        #region Métodos

        /// <summary>
        /// Crea un fichero a partir de la ruta y el array de bytes pasados como parámetros
        /// </summary>
        /// <param name="file">fichero</param>
        /// <param name="pExtension">Extensión del video</param>
        /// <param name="pNuevoNombre">Nombre del video</param>
        /// <param name="pCarpetaID">Identificador de persona o organización</param>
        /// <param name="pTipoVideo">Indica si el video es personal, de una organización o semántico</param>
        [NonAction]
        private bool CrearFichero(IFormFile pBytes, string pNuevoNombre, Guid pCarpetaID, int pTipoVideo)
        {
            int codigo = 1;
            byte[] pBytes2 = _fileOperationsService.ReadFileBytes(pBytes);
            string rutaFichero;
            DirectoryInfo directorio;
            if (pCarpetaID == Guid.Empty)
            {
                rutaFichero = Path.Combine(mRutaVideos, pNuevoNombre);
                directorio = new DirectoryInfo(mRutaVideos);
            }
            else if (pTipoVideo == 0)
            {
                rutaFichero = Path.Combine(mRutaVideos, "VideosPersonales",  pCarpetaID.ToString(), pNuevoNombre);
                directorio = new DirectoryInfo(Path.Combine(mRutaVideos, "VideosPersonales",  pCarpetaID.ToString()));
            }
            else if (pTipoVideo == 1)
            {
                rutaFichero = Path.Combine(mRutaVideos, "VideosOrganizaciones",  pCarpetaID.ToString(), pNuevoNombre);
                directorio = new DirectoryInfo(Path.Combine(mRutaVideos, "VideosOrganizaciones",  pCarpetaID.ToString()));
            }
            else
            {
                rutaFichero = Path.Combine(mRutaVideos, "VideosSemanticos",  pCarpetaID.ToString(), pNuevoNombre);
                directorio = new DirectoryInfo(Path.Combine(mRutaVideos, "VideosSemanticos",  pCarpetaID.ToString()));
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

            FileStream ficheroAGuardar = null;
            try
            {
                ficheroAGuardar = new FileStream(rutaFichero, FileMode.Create, FileAccess.Write);
                ficheroAGuardar.Write(pBytes2, 0, pBytes2.Length);
                ficheroAGuardar.Flush();
                ficheroAGuardar.Close();
                ficheroAGuardar.Dispose();
                ficheroAGuardar = null;
            }
            catch (Exception ext2)
            {
                throw ext2;
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
        /// <param name="pRuta">ruta de el video</param>
        /// <returns>Espacio que ocupa el video</returns>
        [NonAction]
        private double ObtenerEspacionFichero(string pRuta)
        {
            FileInfo video = new FileInfo(pRuta);
            double tamanoVideo = 0;
            if (video.Exists)
            {
                tamanoVideo = ((double)video.Length) / 1024 / 1024;
            }
            return tamanoVideo;
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
            try
            {
                string rutaVideo;
                if (pCarpetaID == Guid.Empty)
                {
                    rutaVideo = Path.Combine(mRutaVideos, pDocumentoID.ToString() + ".flv");
                }
                else if (pTipoVideo == 0)
                {
                    rutaVideo = Path.Combine(mRutaVideos, "VideosPersonales",  pCarpetaID.ToString(), pDocumentoID.ToString() + ".flv");
                }
                else if (pTipoVideo == 1)
                {
                    rutaVideo = Path.Combine(mRutaVideos, "VideosOrganizaciones",  pCarpetaID.ToString(), pDocumentoID.ToString() + ".flv");
                }
                else
                {
                    rutaVideo = Path.Combine(mRutaVideos, "VideosSemanticos",  pCarpetaID.ToString(), pDocumentoID.ToString() + ".flv");
                }

                //Borramos el fichero físico.
                FileInfo fich = new FileInfo(rutaVideo);
                fich.Delete();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

    }
}

