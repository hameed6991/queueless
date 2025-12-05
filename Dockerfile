# ========= BASE IMAGE (runtime) =========
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# This is the port the app listens on *inside* the container
EXPOSE 8080

# ========= BUILD IMAGE =========
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1) copy csproj and restore
COPY ["Queueless.csproj", "./"]
RUN dotnet restore "Queueless.csproj"

# 2) copy everything and publish
COPY . .
RUN dotnet publish "Queueless.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ========= FINAL IMAGE =========
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Tell ASP.NET Core which URL to bind to INSIDE the container
# Render will override PORT env var, but this keeps it sane
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV PORT=8080

ENTRYPOINT ["dotnet", "Queueless.dll"]
