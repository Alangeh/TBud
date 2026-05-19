# Multi-stage Dockerfile for TravelReview .NET 8 API (SQL Server backend).
# Build:  docker build -t travelreview-api .
# Run:    docker run --rm -p 8001:8001 \
#           -e CONNECTION_STRING='Server=host.docker.internal,1433;Database=TravelReview;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;' \
#           -e JWT_SECRET='your-32-char-secret-here' \
#           travelreview-api
#
# Note: the app applies EF Core migrations automatically on startup
# (Migrations:ApplyOnStartup=true in appsettings.json). Make sure you've run
# `dotnet ef migrations add Initial` locally before building, so the Migrations
# folder is committed and copied into the image.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY TravelReview.Api.csproj ./
RUN dotnet restore TravelReview.Api.csproj
COPY . ./
RUN dotnet publish TravelReview.Api.csproj -c Release -o /app/out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
EXPOSE 8001
ENV ASPNETCORE_URLS=http://0.0.0.0:8001
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENTRYPOINT ["dotnet", "TravelReview.Api.dll"]
