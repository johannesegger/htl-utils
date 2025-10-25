FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
RUN apt update && apt install -y chromium-driver libldap-2.5-0 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-server
WORKDIR /src
COPY ["src/ManageGuestAccounts.Server/ManageGuestAccounts.Server.fsproj", "ManageGuestAccounts.Server/"]
RUN dotnet restore "ManageGuestAccounts.Server/ManageGuestAccounts.Server.fsproj"
COPY src/ .
WORKDIR "/src/ManageGuestAccounts.Server"
RUN dotnet build "ManageGuestAccounts.Server.fsproj" -c Release -o /app/build

FROM build-server AS publish-server
RUN dotnet publish "ManageGuestAccounts.Server.fsproj" -c Release -o /app/publish

FROM node:22 AS build-client
WORKDIR /src
COPY src/ManageGuestAccounts.Client/package.json src/ManageGuestAccounts.Client/package-lock.json ./
RUN npm ci
COPY src/ManageGuestAccounts.Client ./
RUN npm run build

FROM base AS final
WORKDIR /app
COPY --from=publish-server /app/publish .
COPY --from=build-client /src/dist ./wwwroot
ENTRYPOINT ["dotnet", "ManageGuestAccounts.Server.dll"]