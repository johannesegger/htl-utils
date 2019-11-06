FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY Common/Common.fsproj ./Common/
COPY PhotoLibrary/PhotoLibrary.fsproj ./PhotoLibrary/
RUN dotnet restore PhotoLibrary

# Copy everything else and build
COPY Common ./Common
COPY PhotoLibrary ./PhotoLibrary
RUN dotnet publish PhotoLibrary -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["./PhotoLibrary"]