# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first to maximise Docker layer caching.
# The restore step will be skipped on subsequent builds if these files are unchanged.
COPY Exemplar.sln global.json ./

COPY src/Exemplar.Core/Exemplar.Core.csproj                             src/Exemplar.Core/
COPY src/Exemplar.Customers.Contracts/Exemplar.Customers.Contracts.csproj src/Exemplar.Customers.Contracts/
COPY src/Exemplar.Customers/Exemplar.Customers.csproj                   src/Exemplar.Customers/
COPY src/Exemplar.Addresses/Exemplar.Addresses.csproj                   src/Exemplar.Addresses/
COPY src/Exemplar.API/Exemplar.API.csproj                               src/Exemplar.API/

COPY tests/Exemplar.Core.Tests/Exemplar.Core.Tests.csproj               tests/Exemplar.Core.Tests/
COPY tests/Exemplar.Customers.Tests/Exemplar.Customers.Tests.csproj     tests/Exemplar.Customers.Tests/
COPY tests/Exemplar.Addresses.Tests/Exemplar.Addresses.Tests.csproj     tests/Exemplar.Addresses.Tests/

RUN dotnet restore "src/Exemplar.API/Exemplar.API.csproj"

# Copy remaining source and publish in Release mode.
COPY . .
RUN dotnet publish "src/Exemplar.API/Exemplar.API.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for container security.
RUN addgroup --system --gid 1001 appgroup \
 && adduser  --system --uid 1001 --ingroup appgroup --no-create-home appuser

COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser
EXPOSE 8080

ENTRYPOINT ["dotnet", "Exemplar.API.dll"]
