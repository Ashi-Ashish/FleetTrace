# build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ../../FleetTrace.sln ./
COPY ../../src/Shared.Contracts/ ./src/Shared.Contracts/
COPY ../../src/StreamWorker/     ./src/StreamWorker/
RUN dotnet restore ./src/StreamWorker/StreamWorker.csproj
RUN dotnet publish  ./src/StreamWorker/StreamWorker.csproj -c Release -o /app

# runtime
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
# copy published worker
COPY --from=build /app ./
# copy schema into container at /app/contracts
COPY ../../contracts ./contracts
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "StreamWorker.dll"]