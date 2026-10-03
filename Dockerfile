# =====================================================================
# Etapa 1: Compilación y Publicación (.NET 10 SDK)
# =====================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar archivo de proyecto y restaurar paquetes NuGet
COPY ["software_elviejomadero.csproj", "./"]
RUN dotnet restore "software_elviejomadero.csproj"

# Copiar el resto del código fuente y publicar en modo Release
COPY . .
RUN dotnet publish "software_elviejomadero.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =====================================================================
# Etapa 2: Imagen Final de Ejecución (.NET 10 ASP.NET Runtime)
# =====================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Asegurar permisos de lectura/escritura para la base de datos SQLite (viejo_madero.db)
RUN mkdir -p /app/data && chmod -R 777 /app

# Copiar artefactos compilados desde la etapa build
COPY --from=build /app/publish .

# Variables de entorno por defecto (Render sobreescribe PORT dinámicamente)
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "software_elviejomadero.dll"]
