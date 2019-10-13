FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY Common/Common.fsproj ./Common/
COPY Admin.Server/Admin.Server.fsproj ./Admin.Server/
RUN dotnet restore Admin.Server

# Copy everything else and build
COPY Common ./Common
COPY Admin.Server ./Admin.Server
RUN dotnet publish Admin.Server -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["./Admin.Server"]