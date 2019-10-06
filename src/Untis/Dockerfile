FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY Common/Common.fsproj ./Common/
COPY Untis/Untis.fsproj ./Untis/
RUN dotnet restore Untis

# Copy everything else and build
COPY Common ./Common
COPY Untis ./Untis
RUN dotnet publish Untis -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["./Untis"]