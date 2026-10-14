# ==============================================================================
# VisionStudio Headless Industrial Machine Vision Runtime Dockerfile
# Multi-stage build targeting .NET 8 on Linux (Debian 12 Bookworm)
# ==============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project definitions for layer caching
COPY VisionStudio.sln ./
COPY src/VisionStudio.Core/*.csproj ./src/VisionStudio.Core/
COPY src/VisionStudio.Calibration/*.csproj ./src/VisionStudio.Calibration/
COPY src/VisionStudio.Imaging/*.csproj ./src/VisionStudio.Imaging/
COPY src/VisionStudio.Matching/*.csproj ./src/VisionStudio.Matching/
COPY src/VisionStudio.DeepLearning/*.csproj ./src/VisionStudio.DeepLearning/
COPY src/VisionStudio.Scripting/*.csproj ./src/VisionStudio.Scripting/
COPY src/VisionStudio.Training/*.csproj ./src/VisionStudio.Training/
COPY src/VisionStudio.Devices.Abstractions/*.csproj ./src/VisionStudio.Devices.Abstractions/
COPY src/VisionStudio.Devices.Cameras/*.csproj ./src/VisionStudio.Devices.Cameras/
COPY src/VisionStudio.Devices.Plc/*.csproj ./src/VisionStudio.Devices.Plc/
COPY src/VisionStudio.Devices.Robots/*.csproj ./src/VisionStudio.Devices.Robots/
COPY src/VisionStudio.Connectivity/*.csproj ./src/VisionStudio.Connectivity/
COPY src/VisionStudio.Cli/*.csproj ./src/VisionStudio.Cli/
COPY src/VisionStudio.Runtime/*.csproj ./src/VisionStudio.Runtime/

RUN dotnet restore src/VisionStudio.Runtime/VisionStudio.Runtime.csproj

# Copy all source files
COPY . .

# Publish release binary
WORKDIR /app/src/VisionStudio.Runtime
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ==============================================================================
# Production Runtime Stage
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install native OpenCV & graphics dependencies for Linux execution
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1 \
    libglib2.0-0 \
    libgomp1 \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV VISIONSTUDIO_DATA_DIR=/data

# Create data directory mount point
RUN mkdir -p /data && chmod 777 /data
VOLUME [ "/data" ]

# Expose HTTP REST / Prometheus metrics (5000) and OPC UA TCP listener (4840)
EXPOSE 5000
EXPOSE 4840

HEALTHCHECK --interval=15s --timeout=3s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "VisionStudio.Runtime.dll"]
