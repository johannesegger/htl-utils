FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY Common/Common.fsproj ./Common/
COPY AAD/AAD.fsproj ./AAD/
RUN dotnet restore AAD

# Copy everything else and build
COPY Common ./Common
COPY AAD ./AAD
RUN dotnet publish AAD -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["./AAD"]