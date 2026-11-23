FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src
ARG TARGETARCH
ENV PYGMALION_TARGET_ARCH=${TARGETARCH}
COPY Backend-Api/ Backend-Api/
RUN case "$TARGETARCH" in \
      amd64) rid=linux-x64 ;; \
      arm64) rid=linux-arm64 ;; \
      *) echo "Unsupported Docker target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac && dotnet restore Backend-Api/Backend-Api.Tests/Backend-Api.Tests.csproj --runtime "$rid"
CMD ["sh", "-c", "case \"$PYGMALION_TARGET_ARCH\" in amd64) rid=linux-x64 ;; arm64) rid=linux-arm64 ;; *) echo \"Unsupported test target architecture: $PYGMALION_TARGET_ARCH\" >&2; exit 1 ;; esac; exec dotnet test Backend-Api/Backend-Api.Tests/Backend-Api.Tests.csproj --configuration Release --runtime \"$rid\" --no-restore"]
