FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src
COPY Backend-Api/ Backend-Api/
RUN dotnet restore Backend-Api/Backend-Api.Tests/Backend-Api.Tests.csproj --runtime linux-x64
CMD ["dotnet", "test", "Backend-Api/Backend-Api.Tests/Backend-Api.Tests.csproj", "--configuration", "Release", "--runtime", "linux-x64", "--no-restore"]
