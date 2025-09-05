FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-server
WORKDIR /src
COPY ["src/IndividualTests.Server/IndividualTests.Server.fsproj", "IndividualTests.Server/"]
RUN dotnet restore "IndividualTests.Server/IndividualTests.Server.fsproj"
COPY src/ .
WORKDIR "/src/IndividualTests.Server"
RUN dotnet build "IndividualTests.Server.fsproj" -c Release -o /app/build

FROM build-server AS publish-server
RUN dotnet publish "IndividualTests.Server.fsproj" -c Release -o /app/publish

FROM node:24 AS build-client
WORKDIR /src
COPY src/IndividualTests.Client/package.json src/IndividualTests.Client/package-lock.json ./
RUN npm ci
COPY src/IndividualTests.Client ./
RUN npm run build

FROM base AS final
WORKDIR /app
COPY --from=publish-server /app/publish .
COPY --from=build-client /src/dist ./wwwroot
ENTRYPOINT ["dotnet", "IndividualTests.Server.dll"]