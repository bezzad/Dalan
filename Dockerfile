# For local runs and container platforms. Production in Iran runs the binary under systemd (deploy/).
FROM node:26-alpine AS client
WORKDIR /client
COPY client/ ./
RUN npm ci && npm run build

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY global.json Dalan.slnx Directory.Build.props Directory.Packages.props ./
COPY src/ src/
COPY --from=client /client/dist client/dist
RUN dotnet publish src/Dalan.Host/Dalan.Host.csproj -c Release -o /app -p:StaticWebAssetsEnabled=false

FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Dalan.Host.dll"]
