FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Backend-Api/Backend-Api.csproj Backend-Api/
RUN dotnet restore Backend-Api/Backend-Api.csproj --runtime linux-x64
COPY Backend-Api/ Backend-Api/
RUN dotnet publish Backend-Api/Backend-Api.csproj -c Release --runtime linux-x64 --self-contained false -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backend-Api.dll"]
