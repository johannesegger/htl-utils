FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY Common/Common.fsproj ./Common/
COPY FileStorage/FileStorage.fsproj ./FileStorage/
RUN dotnet restore FileStorage

# Copy everything else and build
COPY Common ./Common
COPY FileStorage ./FileStorage
RUN dotnet publish FileStorage -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["./FileStorage"]