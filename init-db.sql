-- Create databases for each microservice to respect the database-per-service principle
CREATE DATABASE fleetflow_auth;
CREATE DATABASE fleetflow_fleet;
CREATE DATABASE fleetflow_booking;

-- Ensure both postgres and admin roles exist with full permissions
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'admin') THEN
        CREATE ROLE admin WITH SUPERUSER LOGIN PASSWORD 'your_password_here';
    END IF;
END $$;

GRANT ALL PRIVILEGES ON DATABASE fleetflow_auth TO admin;
GRANT ALL PRIVILEGES ON DATABASE fleetflow_fleet TO admin;
GRANT ALL PRIVILEGES ON DATABASE fleetflow_booking TO admin;


