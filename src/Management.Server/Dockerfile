FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY Common/Common.fsproj ./Common/
COPY Common.Server/Common.Server.fsproj ./Common.Server/
COPY Management.Server/Management.Server.fsproj ./Management.Server/
RUN dotnet restore Management.Server

# Copy everything else and build
COPY Common ./Common
COPY Common.Server ./Common.Server
COPY Management.Server ./Management.Server
RUN dotnet publish Management.Server -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["./Management.Server"]