# --- Stage 1: Build ---
# We use the heavy .NET SDK image to compile our code
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["GrapholoApi.csproj", "./"]
RUN dotnet restore "GrapholoApi.csproj"

# Copy the rest of the code and build the release version
COPY . .
RUN dotnet publish "GrapholoApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- Stage 2: Runtime ---
# We use the tiny ASP.NET runtime image for the final container
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Expose port 8080 (which matches our docker-compose.yml)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false

# Copy the compiled output from the build stage
COPY --from=build /app/publish .

# Command to run the application
ENTRYPOINT ["dotnet", "GrapholoApi.dll"]