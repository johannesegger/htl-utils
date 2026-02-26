FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt update && apt install -y libldap2 && rm -rf /var/lib/apt/lists/*
RUN apt update && apt install -y --no-install-recommends \
    npm \
    libnspr4 \
    libnss3 \
    libatk1.0-0t64 \
    libatk-bridge2.0-0t64 \
    libcups2t64 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libcairo2 \
    libpango-1.0-0 \
    libasound2t64 && \
    rm -rf /var/lib/apt/lists/* && \
    npx --yes @puppeteer/browsers install chromium
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-server
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