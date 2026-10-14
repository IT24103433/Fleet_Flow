# Disposable Linux rehearsal image; production still uses four separate API artifacts.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/backend/ src/backend/
COPY tools/ tools/
RUN for service in IdentityService FleetService RentalService NotificationService; do \
      dotnet publish src/backend/services/$service/$service.Api/$service.Api.csproj -c Release -o /review/$service --no-self-contained || exit 1; \
    done && \
    dotnet publish tools/NotificationMigration/NotificationMigration.csproj -c Release -o /review/NotificationMigration --no-self-contained && \
    dotnet publish tools/RentalMigration/RentalMigration.csproj -c Release -o /review/RentalMigration --no-self-contained
FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY --from=build /review/ /review/
WORKDIR /review
ENV ASPNETCORE_HTTP_PORTS=8080
# Each API is launched in its own container by the guarded rehearsal runner.
