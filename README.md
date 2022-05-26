# Gnoss.Web.Intern.OpenCORE

Aplicación Web que se encarga de almacenar el contenido estático (imágenes, vídeos y pdfs principalmente) que suben los usuarios desde la Web. Esta aplicación NO debe ser accesible desde el exterior de la plataforma GNOSS, sólo debe estar disponible para que el resto de aplicaciones puedan hacer peticiones Web a ella. 

Configuración estandar de esta aplicación en el archivo docker-compose.yml: 

```yml
interno:
    image: intern
    env_file: .env
    ports:
     - ${puerto_interno}:80
    environment:
     virtuosoConnectionString: ${virtuosoConnectionString}
     acid: ${acid}
     base: ${base}
     redis__redis__ip__master: ${redis__redis__ip__master}
     redis__redis__ip__read: ${redis__redis__ip__read}
     redis__redis__bd: ${redis__redis__bd}
     redis__redis__timeout: ${redis__redis__timeout}
     redis__recursos__ip__master: ${redis__recursos__ip__master}
     redis__recursos__ip__read: ${redis__recursos__ip__read}
     redis__recursos__bd: ${redis__recursos__bd}
     redis__recursos__timeout: ${redis__redis__timeout}
     idiomas: ${idiomas}
     Servicios__urlBase: ${Servicios__urlBase}
     connectionType: ${connectionType}
    volumes:
      - ./logs/interno:/app/logs
      - ./content/doclinks:/app/doclinks
      - ./content/imagenes:/app/imagenes
      - ./content/ImagenesEnlaces:/app/ImagenesEnlaces
      - ./content/Videos:/app/Videos
```

Se pueden consultar los posibles valores de configuración de cada parámetro aquí: https://github.com/equipognoss/Gnoss.Platform.Deploy
