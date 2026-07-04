# syntax=docker/dockerfile:1

ARG DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:latest
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:latest

FROM ${DOTNET_ASPNET_IMAGE} AS base
WORKDIR /app
EXPOSE 8080

FROM ${DOTNET_SDK_IMAGE} AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Tangle.slnx .
COPY services/Social/Social.csproj services/Social/
RUN dotnet restore services/Social/Social.csproj

COPY services/Social/ services/Social/
WORKDIR /src/services/Social

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish Social.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Social.dll"]
