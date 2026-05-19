# Multi-stage Dockerfile for TravelReview .NET 8 API.
# Build:  docker build -t travelreview-api .
# Run:    docker run --rm -p 8001:8001 \
#           -e MONGO_URL='mongodb://host.docker.internal:27017' \
#           -e DB_NAME='travelreview' \
#           -e JWT_SECRET='your-32-char-secret-here' \
#           travelreview-api

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
