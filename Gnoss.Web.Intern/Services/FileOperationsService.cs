using Es.Riam.Gnoss.Util.General;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Gnoss.Web.Intern.Services
{
    public class FileOperationsService
    {
        private LoggingService _loggingService;
        private IHostingEnvironment _env;

        public FileOperationsService(LoggingService loggingService, IHostingEnvironment env)
        {
            _loggingService = loggingService;
            _env = env;
        }

        public byte[] ReadFileBytes(IFormFile file)
        {
            byte[] fileBytes = null;
            using (var ms = new MemoryStream())
            {
                file.CopyTo(ms);
                fileBytes = ms.ToArray();
            }
            return fileBytes;
        }

        /// <summary>
        /// Guarda el log de error
        /// </summary>
        /// <param name="pError">Cadena de texto con el error</param>
        public void GuardarLogError(Exception pError)
        {
            GuardarLogError(_loggingService.DevolverCadenaError(pError, "1.0"));
        }

        /// <summary>
        /// Guarda el log de error
        /// </summary>
        /// <param name="pError">Cadena de texto con el error</param>
        public void GuardarLogError(string pMensaje)
        {
            string directorio = Path.Combine(_env.ContentRootPath, "logs");
            //string directorio = HttpContext.Current.Server.MapPath(HttpContext.Current.Request.ApplicationPath + "/logs");
            string fichero = Path.Combine(directorio, "error_" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");

            if (!Directory.Exists(directorio))
            {
                Directory.CreateDirectory(directorio);
            }

            _loggingService.GuardarLogError(pMensaje, fichero);
        }
    }
}
