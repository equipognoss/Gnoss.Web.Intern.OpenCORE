using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD;
using Es.Riam.Gnoss.AD.EncapsuladoDatos;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS;
using Es.Riam.Gnoss.AD.ParametroAplicacion;
using Es.Riam.Gnoss.AD.ServiciosGenerales;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.CL.ServiciosGenerales;
using Es.Riam.Gnoss.CL.Trazas;
using Es.Riam.Gnoss.Logica.Identidad;
using Es.Riam.Gnoss.Logica.Usuarios;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.Util.Seguridad;
using Es.Riam.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Gnoss.Web.Intern.Controllers
{
    /// <summary>
    /// Descripción breve de ServicioDatos
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class DatosController : ControllerBase
    {
        private Conexion _conexion;
        private LoggingService _loggingService;
        private EntityContext _entityContext;
        private ConfigService _configService;
        protected EntityContext mEntityContext;
        protected LoggingService mLoggingService;
        protected ConfigService mConfigService;
        protected RedisCacheWrapper mRedisCacheWrapper;
        private IServicesUtilVirtuosoAndReplication mServicesUtilVirtuosoAndReplication;
        private static object BLOQUEO_COMPROBACION_TRAZA = new object();
        private static DateTime HORA_COMPROBACION_TRAZA;
        private ILogger mlogger;
        private ILoggerFactory mLoggerFactory;
        #region Constructores

        /// <summary>
        /// Constructor sin parámetros
        /// </summary>
        public DatosController(Conexion conexion, LoggingService loggingService, EntityContext entityContext, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication, ILogger<DatosController> logger, ILoggerFactory loggerFactory)
        {
            _conexion = conexion;
            _loggingService = loggingService;
            _entityContext = entityContext;
            mServicesUtilVirtuosoAndReplication = servicesUtilVirtuosoAndReplication;
            mlogger = logger;
            mLoggerFactory = loggerFactory;

            //Eliminar la marca de comentario de la línea siguiente si utiliza los componentes diseñados 
            //InitializeComponent();
        }

        #endregion

        #region Metodos Web

        [HttpPost]
        [Route("AutoCompletar")]
        public IActionResult AutoCompletar()
        {
            return Ok("Madrid, España" + Environment.NewLine + "Barcelona, España" + Environment.NewLine + "Londres, Inglaterra" + Environment.NewLine + "Habana, Cuba" + Environment.NewLine + "Mexico, Mexico" + Environment.NewLine);
        }


        /// <summary>
        /// Obtiene la URL del servicio de documentación    
        /// </summary>
        /// <returns>URL del servicio de documentación</returns>
        [HttpGet]
        [Route("ObtenerUrlGestorDocumental")]
        public IActionResult ObtenerUrlGestorDocumental()
        {
            string urlServicioWebDocumentacion;

            List<ParametroAplicacion> filas = _entityContext.ParametroAplicacion.Where(parametroApp => parametroApp.Parametro.Equals(TiposParametrosAplicacion.UrlServicioWebDocumentacion)).ToList();

            if (filas.Count > 0)
            {
                ParametroAplicacion fila = filas[0];
                urlServicioWebDocumentacion = fila.Valor;
            }
            else
            {
                urlServicioWebDocumentacion = "";
            }

            return Ok(urlServicioWebDocumentacion);
        }

        /// <summary>
        /// Añade al perfil del usuario el gadget de ideas4all
        /// </summary>
        [HttpGet]
        [Route("ActualizarPerfilUsuario")]
        public void ActualizarPerfilUsuario(Guid pUsuarioID, Guid pPerfilID)
        {
            //IdentidadCN identidadCN = new IdentidadCN("acid", true);
            IdentidadCN identidadCN = new IdentidadCN(_entityContext, _loggingService, _configService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<IdentidadCN>(), mLoggerFactory);
            if (pPerfilID.Equals(Guid.Empty))
            {
                UsuarioCN usuarioCN = new UsuarioCN(_entityContext, _loggingService, _configService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<UsuarioCN>(), mLoggerFactory);
                string login = usuarioCN.ObtenerUsuarioPorID(pUsuarioID).Login;
                usuarioCN.Dispose();
                pPerfilID = identidadCN.ObtenerIdentidadIDDeUsuarioEnProyectoYOrg(login, ProyectoAD.MetaProyecto, string.Empty, false)[1];
            }
            DataWrapperIdentidad identidadDW = identidadCN.ObtenerGadgetsPerfil(pPerfilID);

            List<PerfilGadget> filasPerfilGadget = identidadDW.ListaPerfilGadget.Where(item => item.Titulo.Equals("Ideas4all")).ToList();
            if (filasPerfilGadget.Count == 0)
            {
                #region Añadimos el gadget

                PerfilGadget filaPerfilGadget = new PerfilGadget();

                filaPerfilGadget.PerfilID = pPerfilID;
                filaPerfilGadget.GadgetID = Guid.NewGuid();
                filaPerfilGadget.Titulo = "Ideas4all";
                filaPerfilGadget.Contenido = "<div id=\"ideas4all-widget\"></div><script src=\"\"></script>";
                filaPerfilGadget.Orden = (short)(identidadDW.ListaPerfilGadget.Count);

                identidadDW.ListaPerfilGadget.Add(filaPerfilGadget);
                _entityContext.PerfilGadget.Add(filaPerfilGadget);
                #endregion

                identidadCN.ActualizaIdentidades();
            }
            identidadCN.Dispose();
        }

        /// <summary>
        /// Obtiene los tokens de la aplicación que se conecta a twitter para este dominio
        /// </summary>
        /// <returns>Array con los tokens de la aplicación twitter para este dominio de la siguiente manera: [consumerKey, consumerSecret]</returns>
        [HttpGet]
        [Route("ObtenerTokensAplicacionTwitter")]
        public IActionResult ObtenerTokensAplicacionTwitter()
        {
            XmlDocument documentoXml = new XmlDocument();
            string consumerKey = "";
            string consumerSecret = "";
            consumerKey = _conexion.ObtenerParametro("config/GnossRedesSociales.config", "TwitterConsumerKey", false);
            consumerSecret = _conexion.ObtenerParametro("config/GnossRedesSociales.config", "TwitterConsumerSecret", false);
            string[] array = { consumerKey, consumerSecret };
            return Ok(array);
        }

        /// <summary>
        /// Invalida la cache de el tesauro de un proyecto
        /// </summary>
        /// <param name="pOrganizacionID">Identificador de la organización que tiene el tesauro</param>
        /// <param name="pProyectoID">Identificador del proyecto que tiene el tesauro</param>
        /// <param name="pTesauroDS">Dataset de tesauro actualizado</param>
        [HttpGet]
        [Route("InvalidarCacheTesauro")]
        public void InvalidarCacheTesauro(Guid pProyectoID)
        {
            /*
            TesauroCL tesauroCL = new TesauroCL();
            tesauroCL.InvalidarCacheDeTesauroDeProyecto(pProyectoID);
            */
        }

        #endregion

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
                        TrazasCL trazasCL = new TrazasCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication, mLoggerFactory.CreateLogger<TrazasCL>(), mLoggerFactory);
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
        public virtual void OnActionExecuting(ActionExecutingContext filterContext)
        {
            IniciarTraza();
        }
    }

}