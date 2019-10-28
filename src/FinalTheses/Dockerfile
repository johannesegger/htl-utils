FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY Common/Common.fsproj ./Common/
COPY FinalTheses/FinalTheses.fsproj ./FinalTheses/
RUN dotnet restore FinalTheses

# Copy everything else and build
COPY Common ./Common
COPY FinalTheses ./FinalTheses
RUN dotnet publish FinalTheses -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["./FinalTheses"]