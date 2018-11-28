FROM microsoft/dotnet:2.1.4-runtime-stretch-slim-arm32v7
# FROM microsoft/dotnet:2.1.4-runtime-stretch-slim
COPY /deploy /
WORKDIR /Server
EXPOSE 5000
EXPOSE 5001
ENTRYPOINT [ "dotnet", "Server.dll" ]