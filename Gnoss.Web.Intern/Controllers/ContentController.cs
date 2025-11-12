using BeetleX;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.InterfacesOpenArchivos;
using Es.Riam.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.IO;
using System.Net.Mime;

namespace Gnoss.Web.Intern.Controllers
{
    [Route("content/{*redirect}")]
    public class ContentController : ControllerBaseIntern
    {
        private static string mDirectorioPrincipal;
        private const string CONTENT_RELATIVE_PATH = "/content/";
        private new readonly ILogger mLogger;

        public ContentController(LoggingService loggingService, IHostingEnvironment env, ConfigService configService, RedisCacheWrapper redisCacheWrapper, ILogger<ContentController> logger, ILoggerFactory loggerFactory):base(loggingService, redisCacheWrapper, configService, loggerFactory)
        {
            mLogger = logger;

            if (string.IsNullOrEmpty(mDirectorioPrincipal))
            {
                string rutaImagenes = configService.GetRutaImagenes();
                if (string.IsNullOrEmpty(rutaImagenes))
                {
                    rutaImagenes = Path.Combine(env.ContentRootPath, UtilArchivos.ContentImagenes);
                }

                mDirectorioPrincipal = rutaImagenes.Substring(0, rutaImagenes.LastIndexOf(Path.DirectorySeparatorChar));
            }
        }
        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                FileInfo file = GetFileInfo(Request.Path.Value);
                if (file != null && file.Exists)
                {
                    string contentType;
                    new FileExtensionContentTypeProvider().TryGetContentType(file.Name, out contentType);

                    DateTimeOffset? lastModified = DateTime.SpecifyKind(file.LastWriteTimeUtc, DateTimeKind.Utc);
                    long etagHash = lastModified.Value.Ticks ^ file.Length;

                    EntityTagHeaderValue etag = new EntityTagHeaderValue('\"' + Convert.ToString(etagHash, 16) + '\"');

                    return File(file.OpenRead(), contentType, lastModified, etag);
                }
                else
                {
                    mLoggingService.GuardarLogError($"El archivo {Request.Path.Value} no existe",mLogger);
                }
            }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex, $"Error al obtener el archivo {Request.Path.Value}", mLogger);
            }

            return NotFound();
        }

        [NonAction]
        private FileInfo GetFileInfo(string path)
        {
            FileInfo file = null;
            if ((path.Contains(UtilArchivos.ContentImagenes)) || (path.Contains(UtilArchivos.ContentDocLinks)) || (path.Contains(UtilArchivos.ContentVideos)) || (path.Contains(UtilArchivos.ContentImagenesEnlaces)))
            {
                file = new FileInfo(Path.Combine(mDirectorioPrincipal, path.Replace(CONTENT_RELATIVE_PATH, "")));
            }
            return file;
        }
    }
}
