FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY Common/Common.fsproj ./Common/
COPY Common.Server/Common.Server.fsproj ./Common.Server/
COPY Teaching.Server/Teaching.Server.fsproj ./Teaching.Server/
RUN dotnet restore Teaching.Server

# Copy everything else and build
COPY Common ./Common
COPY Common.Server ./Common.Server
COPY AAD/DataTransferTypes.fs ./AAD/
COPY FileStorage/DataTransferTypes.fs ./FileStorage/
COPY PhotoLibrary/DataTransferTypes.fs ./PhotoLibrary/
COPY Sokrates/DataTransferTypes.fs ./Sokrates/
COPY Teaching.Server ./Teaching.Server
RUN dotnet publish Teaching.Server -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["./Teaching.Server"]