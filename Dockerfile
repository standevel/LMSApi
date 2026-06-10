# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["LMS.Api.csproj", "./"]
RUN dotnet restore "LMS.Api.csproj"

# Copy source code and build
COPY . .
RUN dotnet publish "LMS.Api.csproj" -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Create storage directories
RUN mkdir -p /app/LMS_Storage /app/uploads /app/Logs

# Copy published app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LMS.Api.dll"]
