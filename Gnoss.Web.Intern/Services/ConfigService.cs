using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.IO;

namespace ServicioArchivo.Models.Services
{
    public class ConfigService
    {
        public IConfigurationRoot Configuration { get; set; }
        private string cadenaConexion;
        
        public ConfigService()
        {
            var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json");

            Configuration = builder.Build();
        }

        public string GetCadenaConexion()
        {
            if (string.IsNullOrEmpty(cadenaConexion))
            {
                IDictionary environmentVariables = Environment.GetEnvironmentVariables();
                if (environmentVariables.Contains("CadenaConexion"))
                {
                    cadenaConexion = environmentVariables["CadenaConexion"] as string;
                }
                else
                {
                    cadenaConexion = Configuration.GetConnectionString("CadenaConexion");
                }
            }
            return cadenaConexion;
        }

        public string ObtenerCadenaConexion(string cadena)
        {
            if (string.IsNullOrEmpty(cadenaConexion))
            {
                IDictionary environmentVariables = Environment.GetEnvironmentVariables();
                if (environmentVariables.Contains(cadena))
                {
                    cadenaConexion = environmentVariables[cadena] as string;
                }
                else
                {
                    cadenaConexion = Configuration.GetConnectionString(cadena);
                }
            }
            return cadenaConexion;
        }

    }
}
