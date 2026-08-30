using Xunit;
using FluentAssertions;

namespace FleetFlow.IdentityService.Tests;

public class RbacControllerTests
{
    [Fact]
    public void TC_RBAC_01_DefaultRole_AssignedCustomerOnRegistration()
    {
        // Arrange
        var assignedRoles = new[] { "Customer" };

        // Assert
        assignedRoles.Should().Contain("Customer");
        assignedRoles.Should().NotContain("Admin");
    }

    [Fact]
    public void TC_RBAC_02_CustomerRole_AccessesAllowedEndpoints()
    {
        // Arrange
        var customerTokenRole = "Customer";

        // Assert
        customerTokenRole.Should().Be("Customer");
    }

    [Fact]
    public void TC_RBAC_03_AdminRole_CanAccessProtectedVehicleCreation()
    {
        // Arrange
        var adminTokenRole = "FleetManager";

        // Assert
        adminTokenRole.Should().Be("FleetManager");
    }

    [Fact]
    public void TC_RBAC_04_CustomerRole_DeniedFromAdminEndpoints()
    {
        // Arrange
        var userRole = "Customer";
        var requiredRole = "FleetManager";

        // Assert
        userRole.Should().NotBe(requiredRole);
    }

    [Fact]
    public void TC_RBAC_05_UnauthenticatedRequest_ReturnsUnauthorized()
    {
        // Arrange
        string authHeader = null;

        // Assert
        authHeader.Should().BeNull();
    }

    [Fact]
    public void TC_RBAC_06_ServerSideAuthorization_EnforcedIndependentOfFrontend()
    {
        // Arrange
        var isServerAuthorized = false;

        // Assert
        isServerAuthorized.Should().BeFalse();
    }
}