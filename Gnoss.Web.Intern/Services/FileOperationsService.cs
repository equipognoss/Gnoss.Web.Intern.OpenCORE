using Es.Riam.Gnoss.Util.General;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        private ILogger<FileOperationsService> _logger;

        public FileOperationsService(LoggingService loggingService, IHostingEnvironment env)
        {
            _loggingService = loggingService;
            _env = env;
            _logger = loggingService.CrearLogger<FileOperationsService>();
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
    }
}
