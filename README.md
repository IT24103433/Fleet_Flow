# FleetFlow

FleetFlow is a microservice-oriented university project designed to manage vehicle fleets.

## Repository Structure

- `src/backend/services/IdentityService/`: User management, authentication, and RBAC logic.
- `src/backend/services/FleetService/`: Fleet management, vehicle inventory, and details.
- `src/frontend/`: React + Vite single-page application.
- `docker-compose.yml`: Local PostgreSQL database environment for development.

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download)
- [Node.js v22.x](https://nodejs.org/)
- [Docker & Docker Compose](https://www.docker.com/)

## Local Development Setup

### Running Databases with Docker

1. Copy `.env.example` to `.env`.
2. Start the PostgreSQL instance:
   ```bash
   docker compose up -d postgres
   ```

### Running Backend Services

1. Open the solution in Visual Studio or VS Code:
   `src/backend/FleetFlow.sln`
2. Run the services:
   - **IdentityService.Api**: Runs on port 5001
   - **FleetService.Api**: Runs on port 5002

### Running Frontend

1. Navigate to the frontend directory:
   ```bash
   cd src/frontend
   ```
2. Install dependencies:
   ```bash
   npm install
   ```
3. Run the development server:
   ```bash
   npm run dev
   ```