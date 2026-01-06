# Ticketing API - Docker Compose Setup Guide

This guide explains how to set up and run the Ticketing API using Docker Compose.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop) installed and running
- [Docker Compose](https://docs.docker.com/compose/install/) (included with Docker Desktop)
- Git (for cloning the repository)

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/simkinsws/Ticketing.Api.git
cd Ticketing.Api
```

### 2. Start the Services

```bash
docker-compose up -d
```

This command will:
- Build the Ticketing API Docker image
- Start the SQL Server database container
- Start the API application container
- Create and initialize the database with migrations

**Expected output:**
```
Creating network "ticketing-api_default" with the default driver
Creating ticketing-api_sqlserver_1 ... done
Creating ticketing-api_api_1 ... done
```

### 3. Initial Admin Account Setup

The application will automatically seed the database with an initial admin account:

- **Username:** `admin@local.test`
- **Password:** `ChangeMe123!ChangeMe123!` (temporary password)

**⚠️ Important:** This is a temporary password. You must change it on first login.

### 4. Access the Application

#### API Documentation (Swagger)
```
http://localhost:5000/swagger/index.html
```

#### Change Admin Password

1. Open your terminal/PowerShell
2. Use the API to change the password:

```bash
# Login with the default credentials
$loginBody = @{
    email = "admin@local.test"
    password = "ChangeMe123!ChangeMe123!"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body $loginBody

# Change the password
$newPassword = "YourNewSecurePassword123!"

$changePasswordBody = @{
    currentPassword = "ChangeMe123!ChangeMe123!"
    newPassword = $newPassword
    confirmPassword = $newPassword
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/auth/change-password" `
    -Method Post `
    -ContentType "application/json" `
    -Body $changePasswordBody `
    -Headers @{Authorization = "Bearer $($loginResponse.accessToken)"}
```

Or use Swagger UI:
1. Navigate to `http://localhost:5000/swagger/index.html`
2. Click **Authorize** and login with default credentials
3. Find the **change-password** endpoint
4. Enter your new password
5. Click **Execute**

## Docker Compose Configuration

The `docker-compose.yml` file contains:

### SQL Server Service
- **Container Name:** `ticketing-sqlserver`
- **Image:** `mcr.microsoft.com/mssql/server:2022-latest`
- **Port:** `1433` (internal), mapped to host
- **Default Admin Password:** `SA_PASSWORD=YourPassword123!` (change in production)
- **Default Database:** `TicketingDb`

### API Service
- **Container Name:** `ticketing-api`
- **Port:** `5000` (HTTP)
- **Environment:**
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `ConnectionStrings__Default` - Database connection string
  - `Jwt__Key` - JWT signing key
  - Other configuration from `appsettings.Production.json`

## Common Commands

### View Running Containers
```bash
docker-compose ps
```

### View Container Logs
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f api
docker-compose logs -f sqlserver
```

### Stop Services
```bash
docker-compose stop
```

### Stop and Remove Containers
```bash
docker-compose down
```

### Stop and Remove Everything (including volumes/data)
```bash
docker-compose down -v
```

### Rebuild Images
```bash
docker-compose build --no-cache
docker-compose up -d
```

### Restart Services
```bash
docker-compose restart
```
## Troubleshooting

### Port Already in Use

If port 5000 or 1433 is already in use:

```yaml
# In docker-compose.yml, modify the ports section:
services:
  api:
    ports:
      - "5001:80"  # Change 5000 to 5001
  
  sqlserver:
    ports:
      - "1434:1433"  # Change 1433 to 1434
```

Then update your connection strings accordingly.

### Container Won't Start

```bash
# Check logs for errors
docker-compose logs api

# Ensure database is ready (may take 30-60 seconds)
docker-compose logs sqlserver
```

### Database Connection Fails

1. Ensure SQL Server container is fully started
2. Verify connection string in `appsettings.json`
3. Check firewall settings
4. Confirm port mapping is correct

### Permission Denied (Linux/Mac)

```bash
# Run Docker commands with sudo
sudo docker-compose up -d

# Or add your user to docker group
sudo usermod -aG docker $USER
```

## Development vs Production

### Development (Local)
- Use `docker-compose.yml` for basic setup
- SQL Server in Development mode
- Swagger UI enabled
- Logging level: Debug

### Production
- Create `docker-compose.prod.yml` with:
  - Secure passwords in `.env` file
  - Production SQL Server image
  - Swagger UI disabled
  - Logging level: Warning
  - Resource limits defined
  - Health checks configured

Example `docker-compose.prod.yml`:

```yaml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: ${ACCEPT_EULA}
      SA_PASSWORD: ${SA_PASSWORD}
    ports:
      - "1433:1433"
    volumes:
      - sqlserver_data:/var/opt/mssql/data
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SA_PASSWORD} -Q "SELECT 1"
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - ticketing-network

  api:
    image: ticketing-api:latest
    build:
      context: .
      dockerfile: Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: ${ConnectionStrings__Default}
      Jwt__Key: ${Jwt__Key}
      Jwt__Issuer: ${Jwt__Issuer}
      Jwt__Audience: ${Jwt__Audience}
      SendGridSettings__ApiKey: ${SendGridSettings__ApiKey}
      SendGridSettings__FromEmail: ${SendGridSettings__FromEmail}
      SendGridSettings__FromName: ${SendGridSettings__FromName}
    ports:
      - "80:80"
    depends_on:
      sqlserver:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - ticketing-network

volumes:
  sqlserver_data:

networks:
  ticketing-network:
    driver: bridge
```

Run production compose:

```bash
docker-compose -f docker-compose.prod.yml --env-file .env.prod up -d
```

## Security Notes

⚠️ **Important Security Considerations:**

1. **Never commit `.env` files** with real passwords to Git
2. **Change default SQL Server password** before production deployment
3. **Use strong, unique passwords** for all accounts
4. **Rotate JWT keys** regularly in production
5. **Enable SSL/TLS** for production deployments
6. **Use secrets management** (Docker Secrets, Kubernetes Secrets) in production
7. **Limit container resources** in production:

```yaml
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M
```

## Backup and Restore Database

### Backup Database

```bash
docker exec ticketing-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourPassword123!" -Q "BACKUP DATABASE TicketingDb TO DISK = '/var/opt/mssql/backup/TicketingDb.bak'"
```

### Restore Database

```bash
docker exec ticketing-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourPassword123!" -Q "RESTORE DATABASE TicketingDb FROM DISK = '/var/opt/mssql/backup/TicketingDb.bak'"
```

## Additional Resources

- [Docker Documentation](https://docs.docker.com/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [SQL Server on Docker](https://learn.microsoft.com/en-us/sql/linux/quickstart-install-connect-docker)
- [ASP.NET Core Docker Documentation](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images)

## Support

For issues or questions:
1. Check the logs: `docker-compose logs`
2. Review this guide's Troubleshooting section
3. Open an issue on [GitHub](https://github.com/simkinsws/Ticketing.Api/issues)
