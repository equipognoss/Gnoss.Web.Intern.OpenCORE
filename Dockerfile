FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env

RUN sed -i "s|MinProtocol = TLSv1.2|MinProtocol = TLSv1|g" /etc/ssl/openssl.cnf && \
    sed -i 's|CipherString = DEFAULT@SECLEVEL=2|CipherString = DEFAULT@SECLEVEL=1|g' /etc/ssl/openssl.cnf

RUN apt-get update && apt-get install -y --no-install-recommends curl

WORKDIR /app

COPY Gnoss.Web.Intern/*.csproj ./

RUN dotnet restore

COPY . ./

RUN dotnet publish Gnoss.Web.Intern/Gnoss.Web.Intern.csproj -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:6.0

RUN sed -i "s|MinProtocol = TLSv1.2|MinProtocol = TLSv1|g" /etc/ssl/openssl.cnf && \
    sed -i 's|CipherString = DEFAULT@SECLEVEL=2|CipherString = DEFAULT@SECLEVEL=1|g' /etc/ssl/openssl.cnf

RUN apt-get update && apt-get install -y --no-install-recommends curl

RUN apt-get install -y unzip

RUN apt-get install -y nginx

COPY default /etc/nginx/sites-enabled/

COPY nginx.conf /etc/nginx/

WORKDIR /app

COPY --from=build-env /app/out .

ENTRYPOINT ["dotnet", "Gnoss.Web.Intern.dll"]
