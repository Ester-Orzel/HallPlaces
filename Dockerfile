# ---------- Stage 1: build (the big SDK image, thrown away after the build) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish HallPlaces.API/HallPlaces.API.csproj -c Release -o /app/publish

# ---------- Stage 2: run (small runtime-only image) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render sends traffic to port 10000 by default.
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "HallPlaces.API.dll"]
