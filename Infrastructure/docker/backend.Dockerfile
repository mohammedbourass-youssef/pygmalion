FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ARG TARGETARCH
COPY Backend-Api/Backend-Api.csproj Backend-Api/
RUN case "$TARGETARCH" in \
      amd64) rid=linux-x64 ;; \
      arm64) rid=linux-arm64 ;; \
      *) echo "Unsupported Docker target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac && dotnet restore Backend-Api/Backend-Api.csproj --runtime "$rid"
COPY Backend-Api/ Backend-Api/
RUN case "$TARGETARCH" in \
      amd64) rid=linux-x64 ;; \
      arm64) rid=linux-arm64 ;; \
      *) echo "Unsupported Docker target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac && dotnet publish Backend-Api/Backend-Api.csproj -c Release --runtime "$rid" --self-contained false -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backend-Api.dll"]
