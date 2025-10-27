# Azure Deployment Guide for Car Rental API

This guide provides step-by-step instructions for deploying the Car Rental API to Azure using Container Apps.

## Prerequisites

1. **Azure CLI** - Install from [Azure CLI Documentation](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
2. **Docker** - Install from [Docker Documentation](https://docs.docker.com/get-docker/)
3. **PowerShell** (for Windows) or **Bash** (for Linux/Mac)
4. **Azure Subscription** with appropriate permissions

## Quick Deployment

### Option 1: Automated Deployment (Recommended)

1. **Clone the repository and navigate to the project directory**
   ```bash
   cd aegis-ao-rental
   ```

2. **Run the deployment script**
   ```powershell
   # For Windows PowerShell
   .\deploy-azure.ps1 -ResourceGroupName "car-rental-rg" -Location "East US"
   
   # For Linux/Mac Bash
   chmod +x deploy-azure.sh
   ./deploy-azure.sh -ResourceGroupName "car-rental-rg" -Location "East US"
   ```

### Option 2: Manual Deployment

1. **Login to Azure**
   ```bash
   az login
   ```

2. **Create a resource group**
   ```bash
   az group create --name car-rental-rg --location "East US"
   ```

3. **Deploy the infrastructure**
   ```bash
   az deployment group create \
     --resource-group car-rental-rg \
     --template-file azure-infrastructure.json \
     --parameters appName=car-rental-api environment=production
   ```

4. **Build and push Docker image**
   ```bash
   # Get the container registry name from the deployment output
   az acr login --name <your-registry-name>
   docker build -t <your-registry>.azurecr.io/car-rental-api:latest .
   docker push <your-registry>.azurecr.io/car-rental-api:latest
   ```

## Architecture Overview

The deployment creates the following Azure resources:

- **Azure Container Apps** - Hosts the .NET 9.0 API
- **Azure Container Registry** - Stores Docker images
- **Azure Database for PostgreSQL** - Database for the application
- **Azure Log Analytics** - Centralized logging
- **Azure Application Insights** - Application monitoring
- **Azure Key Vault** - Secure storage for secrets

## Configuration

### Environment Variables

The application is configured with the following environment variables:

#### Database Configuration
- `Database__Host` - PostgreSQL server hostname
- `Database__Port` - PostgreSQL port (5432)
- `Database__Database` - Database name (aegis_ao_car_rentals)
- `Database__Username` - Database username
- `Database__Password` - Database password (stored as secret)
- `Database__SSLMode` - SSL mode (Require)

#### JWT Configuration
- `JwtSettings__SecretKey` - JWT signing key (stored as secret)
- `JwtSettings__Issuer` - JWT issuer
- `JwtSettings__Audience` - JWT audience
- `JwtSettings__ExpiryMinutes` - Token expiry time

#### Application Configuration
- `ASPNETCORE_ENVIRONMENT` - Environment (Production)
- `ASPNETCORE_URLS` - Application URLs
- `AllowedHosts` - Allowed hosts

## Monitoring and Logging

### View Application Logs
```bash
az containerapp logs show --name car-rental-api-app --resource-group car-rental-rg --follow
```

### Application Insights
- Navigate to the Application Insights resource in the Azure portal
- View application performance metrics, logs, and traces

### Container App Metrics
- Navigate to the Container App in the Azure portal
- View scaling metrics, request counts, and performance data

## Scaling

### Manual Scaling
```bash
az containerapp update \
  --name car-rental-api-app \
  --resource-group car-rental-rg \
  --min-replicas 2 \
  --max-replicas 10
```

### Auto-scaling
The application is configured with HTTP-based auto-scaling:
- **Min replicas**: 1
- **Max replicas**: 10
- **Scale trigger**: 30 concurrent requests

## Security

### Secrets Management
- Database passwords and JWT secrets are stored in Azure Key Vault
- Container Apps automatically inject secrets as environment variables

### Network Security
- PostgreSQL server is configured with SSL/TLS encryption
- Container Apps use HTTPS for external traffic
- Firewall rules restrict database access

### Authentication
- JWT-based authentication is configured
- Tokens expire after 30 minutes in production
- Secure key rotation is recommended

## CI/CD with GitHub Actions

The repository includes a GitHub Actions workflow for automated deployment:

1. **Set up GitHub Secrets**:
   - `AZURE_CREDENTIALS` - Azure service principal credentials
   - `REGISTRY_USERNAME` - Container registry username
   - `REGISTRY_PASSWORD` - Container registry password

2. **Configure the workflow**:
   - Update the environment variables in `.github/workflows/azure-deploy.yml`
   - Set the correct resource group and container app names

3. **Trigger deployment**:
   - Push to the `main` branch to trigger automatic deployment

## Troubleshooting

### Common Issues

1. **Container App not starting**
   - Check logs: `az containerapp logs show --name car-rental-api-app --resource-group car-rental-rg`
   - Verify environment variables are set correctly
   - Check database connectivity

2. **Database connection issues**
   - Verify PostgreSQL server is running
   - Check firewall rules
   - Validate connection string

3. **Image pull issues**
   - Ensure container registry credentials are correct
   - Verify image exists in the registry
   - Check container registry permissions

### Useful Commands

```bash
# Check deployment status
az deployment group show --resource-group car-rental-rg --name <deployment-name>

# List all resources
az resource list --resource-group car-rental-rg --output table

# Get container app details
az containerapp show --name car-rental-api-app --resource-group car-rental-rg

# Update container app
az containerapp update --name car-rental-api-app --resource-group car-rental-rg --image <new-image>
```

## Cost Optimization

### Recommendations

1. **Use appropriate SKUs**:
   - Container Apps: Start with Basic tier
   - PostgreSQL: Use Burstable tier for development
   - Container Registry: Use Basic tier

2. **Auto-scaling**:
   - Configure appropriate min/max replicas
   - Use HTTP-based scaling rules

3. **Monitoring**:
   - Set up cost alerts
   - Monitor resource usage
   - Review and optimize regularly

## Cleanup

To remove all resources and avoid charges:

```bash
az group delete --name car-rental-rg --yes --no-wait
```

## Support

For issues and questions:
1. Check the application logs
2. Review Azure Container Apps documentation
3. Contact the development team

## Next Steps

1. **Set up custom domain** - Configure a custom domain for the application
2. **Implement monitoring** - Set up comprehensive monitoring and alerting
3. **Security hardening** - Implement additional security measures
4. **Performance optimization** - Optimize application performance
5. **Backup strategy** - Implement database backup and recovery procedures
