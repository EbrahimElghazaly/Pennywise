FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Backend/Pennywise.csproj Backend/
RUN dotnet restore Backend/Pennywise.csproj
COPY Backend/ Backend/
COPY Frontend/ Frontend/
RUN dotnet publish Backend/Pennywise.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /data && chown 1654:1654 /data
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=5000
ENV PENNYWISE_DB_PATH=/data/pennywise.db
EXPOSE 5000 10000
USER 1654
ENTRYPOINT ["dotnet", "Pennywise.dll"]
