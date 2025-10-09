using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;
using AutoFixture;
using AutoFixture.Xunit2;
using Platform.Engineering.Copilot.API.Controllers;
using Platform.Engineering.Copilot.API.Services;
using Platform.Engineering.Copilot.API.Models;

namespace Platform.Engineering.Copilot.Tests.Unit.Platform.API;

public class ToolsControllerTests
{
    private readonly Mock<ILogger<ToolsController>> _mockLogger;
    private readonly Mock<PlatformToolService> _mockToolService;
    private readonly Fixture _fixture;

    public ToolsControllerTests()
    {
        _mockLogger = new Mock<ILogger<ToolsController>>();
        _mockToolService = new Mock<PlatformToolService>();
        _fixture = new Fixture();
    }

    private ToolsController CreateController()
    {
        return new ToolsController(_mockLogger.Object, _mockToolService.Object);
    }

    [Fact]
    public async Task GetTools_ReturnsOkResult_WithTools()
    {
        // Arrange
        var controller = CreateController();
        var expectedResponse = _fixture.Create<ToolListResponse>();
        
        _mockToolService.Setup(x => x.GetAvailableToolsAsync())
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await controller.GetTools();

        // Assert
        result.Should().BeOfType<ActionResult<ToolListResponse>>();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task GetTools_WhenServiceThrows_ReturnsInternalServerError()
    {
        // Arrange
        var controller = CreateController();
        
        _mockToolService.Setup(x => x.GetAvailableToolsAsync())
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await controller.GetTools();

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
    }

    [Theory]
    [AutoData]
    public async Task ExecuteTool_WithValidRequest_ReturnsOkResult(ToolExecutionRequest request)
    {
        // Arrange
        var controller = CreateController();
        var expectedResponse = new ToolExecutionResponse
        {
            Success = true,
            Result = "Tool executed successfully"
        };
        
        _mockToolService.Setup(x => x.ExecuteToolAsync(It.IsAny<ToolExecutionRequest>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await controller.ExecuteTool(request);

        // Assert
        result.Should().BeOfType<ActionResult<ToolExecutionResponse>>();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Theory]
    [AutoData]
    public async Task ExecuteTool_WithFailedExecution_ReturnsBadRequest(ToolExecutionRequest request)
    {
        // Arrange
        var controller = CreateController();
        var expectedResponse = new ToolExecutionResponse
        {
            Success = false,
            Error = "Tool execution failed"
        };
        
        _mockToolService.Setup(x => x.ExecuteToolAsync(It.IsAny<ToolExecutionRequest>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await controller.ExecuteTool(request);

        // Assert
        result.Should().BeOfType<ActionResult<ToolExecutionResponse>>();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task ExecuteTool_WithEmptyToolName_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        var request = new ToolExecutionRequest { ToolName = "" };

        // Act
        var result = await controller.ExecuteTool(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Tool name is required");
    }

    [Fact]
    public async Task ExecuteTool_WhenServiceThrows_ReturnsInternalServerError()
    {
        // Arrange
        var controller = CreateController();
        var request = _fixture.Create<ToolExecutionRequest>();
        
        _mockToolService.Setup(x => x.ExecuteToolAsync(It.IsAny<ToolExecutionRequest>()))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await controller.ExecuteTool(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
        
        var responseValue = objectResult.Value.Should().BeOfType<ToolExecutionResponse>().Subject;
        responseValue.Success.Should().BeFalse();
        responseValue.Error.Should().Be("Internal server error");
    }

    [Theory]
    [AutoData]
    public async Task GetTool_WithExistingTool_ReturnsOkResult(string toolName)
    {
        // Arrange
        var controller = CreateController();
        var toolInfo = new ToolInfo
        {
            Name = toolName,
            Description = "Test tool",
            InputSchema = new Dictionary<string, object>()
        };
        var toolListResponse = new ToolListResponse
        {
            Tools = new List<ToolInfo> { toolInfo }
        };
        
        _mockToolService.Setup(x => x.GetAvailableToolsAsync())
            .ReturnsAsync(toolListResponse);

        // Act
        var result = await controller.GetTool(toolName);

        // Assert
        result.Should().BeOfType<ActionResult<ToolInfo>>();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(toolInfo);
    }

    [Theory]
    [AutoData]
    public async Task GetTool_WithNonExistingTool_ReturnsNotFound(string toolName)
    {
        // Arrange
        var controller = CreateController();
        var toolListResponse = new ToolListResponse
        {
            Tools = new List<ToolInfo>()
        };
        
        _mockToolService.Setup(x => x.GetAvailableToolsAsync())
            .ReturnsAsync(toolListResponse);

        // Act
        var result = await controller.GetTool(toolName);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be($"Tool '{toolName}' not found");
    }

    [Theory]
    [AutoData]
    public async Task GetTool_WhenServiceThrows_ReturnsInternalServerError(string toolName)
    {
        // Arrange
        var controller = CreateController();
        
        _mockToolService.Setup(x => x.GetAvailableToolsAsync())
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await controller.GetTool(toolName);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
    }
}