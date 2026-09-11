# Build API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Equine.sln .
COPY src/Equine.Domain/Equine.Domain.csproj src/Equine.Domain/
COPY src/Equine.Infrastructure/Equine.Infrastructure.csproj src/Equine.Infrastructure/
COPY src/Equine.Api/Equine.Api.csproj src/Equine.Api/
COPY src/Equine.Jobs/Equine.Jobs.csproj src/Equine.Jobs/

RUN dotnet restore

COPY src/Equine.Domain/ src/Equine.Domain/
COPY src/Equine.Infrastructure/ src/Equine.Infrastructure/
COPY src/Equine.Api/ src/Equine.Api/
COPY src/Equine.Jobs/ src/Equine.Jobs/

WORKDIR /src/src/Equine.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Build Angular
FROM node:22-alpine AS web-build
WORKDIR /app

COPY web/package.json web/package-lock.json* web/angular.json ./
COPY web/apps/ ./apps/
COPY web/libs/ ./libs/

RUN npm ci

# We need the API to generate the client, but we can't run it in build stage.
# Copy placeholder API client instead
COPY web/libs/api-client/ ./libs/api-client/

RUN npm run build:admin -- --output-path=dist/admin/browser
RUN npm run build:widget -- --output-path=dist/widget/browser
RUN npx ng build portal --output-path=dist/portal/browser

FROM node:22-alpine AS loader-build
WORKDIR /loader
COPY widget-loader/package.json ./
RUN npm install
COPY widget-loader/ ./
RUN npm run build

# Final image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
COPY --from=web-build /app/dist/admin/browser ./wwwroot/admin
COPY --from=web-build /app/dist/widget/browser ./wwwroot/widget
COPY --from=web-build /app/dist/portal/browser ./wwwroot/portal
COPY --from=loader-build /loader/dist/loader.js ./wwwroot/widget/v1/loader.js

# Serve Angular through the ASP.NET host
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "Equine.Api.dll"]
