FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt update && apt install -y --no-install-recommends \
    curl \
    unzip \
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
    rm -rf /var/lib/apt/lists/*
RUN curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.5/install.sh | bash && \
    . "$HOME/.nvm/nvm.sh" && \
    nvm install 24 && \
    npx --yes @puppeteer/browsers install chromium
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-server
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