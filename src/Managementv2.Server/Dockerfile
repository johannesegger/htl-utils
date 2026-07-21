FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-server
WORKDIR /src
COPY ["src/Managementv2.Server/Managementv2.Server.fsproj", "Managementv2.Server/"]
RUN dotnet restore "Managementv2.Server/Managementv2.Server.fsproj"
COPY src/ .
WORKDIR "/src/Managementv2.Server"
RUN dotnet build "Managementv2.Server.fsproj" -c Release -o /app/build

FROM build-server AS publish-server
RUN dotnet publish "Managementv2.Server.fsproj" -c Release -o /app/publish

FROM node:24 AS build-client
WORKDIR /src
COPY src/Managementv2.Client/package.json src/Managementv2.Client/package-lock.json ./
RUN npm ci
COPY src/Managementv2.Client ./
RUN npm run build

FROM base AS final
WORKDIR /app
COPY --from=publish-server /app/publish .
COPY --from=build-client /src/dist ./wwwroot
ENTRYPOINT ["dotnet", "Managementv2.Server.dll"]
