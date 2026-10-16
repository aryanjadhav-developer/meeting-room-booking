# Stage 1: Build client frontend
FROM node:22-alpine AS client-build
WORKDIR /app/client
COPY client/package*.json ./
RUN npm ci --legacy-peer-deps
COPY client/ ./
RUN npm run build

# Stage 2: Build and publish .NET backend
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS dotnet-build
WORKDIR /src
COPY CabinBooking.sln ./
COPY src/Directory.Build.props ./src/
COPY src/CabinBooking.Domain/CabinBooking.Domain.csproj ./src/CabinBooking.Domain/
COPY src/CabinBooking.Application/CabinBooking.Application.csproj ./src/CabinBooking.Application/
COPY src/CabinBooking.Infrastructure/CabinBooking.Infrastructure.csproj ./src/CabinBooking.Infrastructure/
COPY src/CabinBooking.Api/CabinBooking.Api.csproj ./src/CabinBooking.Api/
RUN dotnet restore src/CabinBooking.Api/CabinBooking.Api.csproj

COPY src/ ./src/
COPY --from=client-build /app/client/dist ./src/CabinBooking.Api/wwwroot
RUN dotnet publish src/CabinBooking.Api/CabinBooking.Api.csproj -c Release -o /app/publish --no-restore -p:TreatWarningsAsErrors=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app
COPY --from=dotnet-build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
ENV Database__Provider=Sqlite
ENV Demo__Enabled=true

EXPOSE 8080
ENTRYPOINT ["dotnet", "CabinBooking.Api.dll"]
