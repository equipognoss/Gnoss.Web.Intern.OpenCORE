using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.CL.Trazas;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;

namespace Gnoss.Web.Intern.Controllers
{
    public class ControllerBaseIntern : Controller
    {
        private static readonly object BLOQUEO_COMPROBACION_TRAZA = new();
        private static DateTime HORA_COMPROBACION_TRAZA;

        protected ILogger mLogger;
        protected ILoggerFactory mLoggerFactory;
        protected LoggingService mLoggingService;
        protected ConfigService mConfigService;
        protected RedisCacheWrapper mRedisCacheWrapper;

        public ControllerBaseIntern(LoggingService loggingService, RedisCacheWrapper redisCacheWrapper, ConfigService configService, ILoggerFactory loggerFactory)
        {
            mLogger = loggerFactory.CreateLogger<ControllerBaseIntern>();
            mLoggerFactory = loggerFactory;
            mLoggingService = loggingService;
            mRedisCacheWrapper = redisCacheWrapper;
            mConfigService = configService;
        }

        #region Métodos de trazas
        [NonAction]
        private void IniciarTraza()
        {
            if (DateTime.Now > HORA_COMPROBACION_TRAZA)
            {
                lock (BLOQUEO_COMPROBACION_TRAZA)
                {
                    if (DateTime.Now > HORA_COMPROBACION_TRAZA)
                    {
                        HORA_COMPROBACION_TRAZA = DateTime.Now.AddSeconds(15);
                        TrazasCL trazasCL = new TrazasCL(null, mLoggingService, mRedisCacheWrapper, mConfigService, null,mLoggerFactory.CreateLogger<TrazasCL>(), mLoggerFactory);
                        string tiempoTrazaResultados = trazasCL.ObtenerTrazaEnCache("intern");

                        if (!string.IsNullOrEmpty(tiempoTrazaResultados))
                        {
                            int valor = 0;
                            int.TryParse(tiempoTrazaResultados, out valor);
                            LoggingService.TrazaHabilitada = true;
                            LoggingService.TiempoMinPeticion = valor; //Para sacar los segundos
                        }
                        else
                        {
                            LoggingService.TrazaHabilitada = false;
                            LoggingService.TiempoMinPeticion = 0;
                        }
                    }
                }
            }
        }
        #endregion
        [NonAction]
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            IniciarTraza();
        }
        [NonAction]
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            mLoggingService.GuardarTraza();
        }
    }
}
