FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-server
WORKDIR /src
COPY ["src/KnowName.Server/KnowName.Server.fsproj", "KnowName.Server/"]
RUN dotnet restore "KnowName.Server/KnowName.Server.fsproj"
COPY src/ .
WORKDIR "/src/KnowName.Server"
RUN dotnet build "KnowName.Server.fsproj" -c Release -o /app/build

FROM build-server AS publish-server
RUN dotnet publish "KnowName.Server.fsproj" -c Release -o /app/publish

FROM node:22 AS build-client
WORKDIR /src
COPY src/KnowName.Client/package.json src/KnowName.Client/package-lock.json ./
RUN npm ci
COPY src/KnowName.Client ./
RUN npm run build

FROM base AS final
WORKDIR /app
COPY --from=publish-server /app/publish .
COPY --from=build-client /src/dist ./wwwroot
ENTRYPOINT ["dotnet", "KnowName.Server.dll"]