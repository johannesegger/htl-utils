FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY Common/Common.fsproj ./Common/
COPY WakeUpComputer/WakeUpComputer.fsproj ./WakeUpComputer/
RUN dotnet restore WakeUpComputer

# Copy everything else and build
COPY Common ./Common
COPY WakeUpComputer ./WakeUpComputer
RUN dotnet publish WakeUpComputer -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["./WakeUpComputer"]