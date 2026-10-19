#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ConnectLive.Core/ConnectLive.Newsletter.Api/ConnectLive.Newsletter.Api.csproj", "ConnectLive.Core/ConnectLive.Newsletter.Api/"]
RUN dotnet restore "ConnectLive.Core/ConnectLive.Newsletter.Api/ConnectLive.Newsletter.Api.csproj"
COPY . .
WORKDIR "/src/ConnectLive.Core/ConnectLive.Newsletter.Api"
RUN dotnet build "ConnectLive.Newsletter.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ConnectLive.Newsletter.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConnectLive.Newsletter.Api.dll"]
