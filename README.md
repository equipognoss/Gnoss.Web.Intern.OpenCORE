![](https://content.gnoss.ws/imagenes/proyectos/personalizacion/7e72bf14-28b9-4beb-82f8-e32a3b49d9d3/cms/logognossazulprincipal.png)

# Gnoss.Web.Intern.OpenCORE

Aplicación Web que se encarga de almacenar el contenido estático (imágenes, vídeos y pdfs principalmente) que suben los usuarios desde la Web. Esta aplicación NO debe ser accesible desde el exterior de la plataforma GNOSS, sólo debe estar disponible para que el resto de aplicaciones puedan hacer peticiones Web a ella. 

Configuración estandar de esta aplicación en el archivo docker-compose.yml: 

```yml
intern:
    image: gnoss/intern
    env_file: .env
    ports:
     - ${puerto_intern}:80
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
      - ./logs/intern:/app/logs
      - ./content/doclinks:/app/doclinks
      - ./content/imagenes:/app/imagenes
      - ./content/ImagenesEnlaces:/app/ImagenesEnlaces
      - ./content/Videos:/app/Videos
```

En esta configuración, existen varios volúmenes que apuntan a las rutas del contenedor:

* /app/doclinks
* /app/imagenes
* /app/ImagenesEnlaces
* /app/Videos

Esos volumenes almacenarán respectivamente las imágenes (/app/imagenes), miniaturas de imágenes (/app/ImagenesEnlaces), vídeos (/app/Videos) y archivos disponibles como contenido estático (/app/doclinks) subidos a la plataforma. Se recomienda realizar copias de seguridad de las unidades en la que se mapeen esos directorios.

Se pueden consultar los posibles valores de configuración de cada parámetro aquí: https://github.com/equipognoss/Gnoss.SemanticAIPlatform.OpenCORE

## Código de conducta
Este proyecto a adoptado el código de conducta definido por "Contributor Covenant" para definir el comportamiento esperado en las contribuciones a este proyecto. Para más información ver https://www.contributor-covenant.org/

## Licencia
Este producto es parte de la plataforma [Gnoss Semantic AI Platform Open Core](https://github.com/equipognoss/Gnoss.SemanticAIPlatform.OpenCORE), es un producto open source y está licenciado bajo GPLv3.
