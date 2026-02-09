# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["Task 4.csproj", "./"]
RUN dotnet restore "Task 4.csproj"

# Copy the entire project and build it
COPY . .
RUN dotnet build "Task 4.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Task 4.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl --fail http://localhost/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "Task 4.dll"]
