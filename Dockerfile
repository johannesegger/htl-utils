FROM microsoft/dotnet:2.1.0-runtime-stretch-slim-arm32v7
COPY /deploy /
WORKDIR /Server
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Server.dll" ]