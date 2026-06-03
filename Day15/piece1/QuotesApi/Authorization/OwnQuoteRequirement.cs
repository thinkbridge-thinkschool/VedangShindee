using Microsoft.AspNetCore.Authorization;

namespace QuotesApi.Authorization;

// Marker requirement: the requesting user must own the quote being acted on.
public class OwnQuoteRequirement : IAuthorizationRequirement { }
