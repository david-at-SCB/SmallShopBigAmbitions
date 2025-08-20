# build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

# runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_ENVIRONMENT=Development
# Configure OTLP endpoint for tracing/logs
ENV OTLP__Endpoint=http://otel-collector:4317
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmallShopBigAmbitions.dll"]