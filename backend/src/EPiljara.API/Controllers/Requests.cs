namespace EPiljara.API.Controllers;

/// <summary>Small request bodies for endpoints where part of the command comes from the route.</summary>
public record UpdateStatusRequest(string Status);

public record SetAvailabilityRequest(bool IsAvailable);
