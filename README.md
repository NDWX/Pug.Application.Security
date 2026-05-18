# Pug.Application.Security

An application-level security abstraction for .NET that decouples business logic from authentication and authorization infrastructure. It provides a principal-centric model where identity retrieval, role checking, and authorization decisions are each defined as pluggable interfaces, unified under a single `ISecurityManager` facade that is async-context-safe.

---

## Solution Structure

| Project | NuGet Package | Purpose |
|---|---|---|
| `Pug.Application.Security.Models` | `Pug.Application.Security.Models` | `Noun` and `NounQualifier` — immutable records that identify a resource by type, identifier, and optional domain |
| `Pug.Application.Security.Abstractions` | `Pug.Application.Security.Abstractions` | All public interfaces (`IPrincipalIdentity`, `IPrincipalIdentityAccessor`, `IPrincipalRoleProvider`, `IAuthorizationProvider`, `IPrincipal`, `ISecurityManager`), the `BasicPrincipalIdentity` concrete type, the `RoleBasedAuthorizationProvider` abstract base, and exception types |
| `Pug.Application.Security` | `Pug.Application.Security` | Concrete `SecurityManager` implementation; wires the three provider interfaces together with per-async-context caching via `AsyncLocal` |
| `Pug.Application.Security.DependencyInjection` | `Pug.Application.Security.DependencyInjection` | `services.AddSecurityManager(appName)` extension method |
| `Pug.Application.Security.PrincipalIdentityAccessors.AspNetCore` | `Pug.Application.Security.PrincipalIdentityAccessors.AspNetCore` | `OAuth20IdentityAccessor` — extracts principal identity from ASP.NET Core `HttpContext.User` JWT claims; `services.AddOAuth20IdentityAccessor()` extension |

---

## How to Use

### 1. Install Packages

For an ASP.NET Core application:

```xml
<PackageReference Include="Pug.Application.Security.DependencyInjection" Version="3.1.0" />
<PackageReference Include="Pug.Application.Security.PrincipalIdentityAccessors.AspNetCore" Version="3.1.0" />
```

For a library that only depends on the contracts:

```xml
<PackageReference Include="Pug.Application.Security.Abstractions" Version="3.1.0" />
```

---

### 2. Implement `IPrincipalRoleProvider`

Supply one implementation that maps a principal identifier to a set of role strings. This is the source of truth for role membership (e.g. a database, an identity provider, or a group membership store).

```csharp
public class MyRoleProvider : IPrincipalRoleProvider
{
    private readonly IDataStore _store;

    public MyRoleProvider(IDataStore store) => _store = store;

    public bool PrincipalIsInRole(string principal, string role) =>
        _store.GetRoles(principal).Contains(role);

    public Task<bool> PrincipalIsInRoleAsync(string principal, string role) =>
        _store.GetRolesAsync(principal)
              .ContinueWith(t => t.Result.Contains(role));

    public bool PrincipalIsInRoles(string principal, ICollection<string> roles) =>
        roles.All(r => PrincipalIsInRole(principal, r));

    public Task<bool> PrincipalIsInRolesAsync(string principal, ICollection<string> roles) =>
        Task.FromResult(PrincipalIsInRoles(principal, roles));

    public IEnumerable<string> GetPrincipalRoles(string principal) =>
        _store.GetRoles(principal);

    public Task<IEnumerable<string>> GetPrincipalRolesAsync(string principal) =>
        _store.GetRolesAsync(principal);
}
```

---

### 3. Implement `IAuthorizationProvider`

#### Option A — Role-based (extend `RoleBasedAuthorizationProvider`)

`RoleBasedAuthorizationProvider` wires the role provider in for you. Override the abstract method to map an operation + resource to a required role.

```csharp
public class MyAuthorizationProvider : RoleBasedAuthorizationProvider
{
    public MyAuthorizationProvider(IPrincipalRoleProvider roleProvider)
        : base(roleProvider) { }

    protected override bool PrincipalIsAuthorized(
        IDictionary<string, string> context,
        IPrincipal principal,
        string operation,
        NounQualifier resource,
        string purpose)
    {
        string requiredRole = $"{resource.Type}.{operation}"; // e.g. "ORDER.CREATE"
        return PrincipalRoleProvider.PrincipalIsInRole(principal.Identity.Identifier, requiredRole);
    }

    protected override Task<bool> PrincipalIsAuthorizedAsync(
        IDictionary<string, string> context,
        IPrincipal principal,
        string operation,
        NounQualifier resource,
        string purpose) =>
        PrincipalRoleProvider.PrincipalIsInRoleAsync(principal.Identity.Identifier, $"{resource.Type}.{operation}");
}
```

#### Option B — Fine-grained ACL (implement `IAuthorizationProvider` directly)

Delegate to an ACL engine, passing the principal identifier, operation, and `NounQualifier` as the resource:

```csharp
public class AclAuthorizationProvider : IAuthorizationProvider
{
    private readonly IAclEngine _acl;

    public AclAuthorizationProvider(IAclEngine acl) => _acl = acl;

    public bool PrincipalIsAuthorized(
        IDictionary<string, string> context,
        IPrincipal principal,
        string operation,
        NounQualifier resource,
        string purpose = "") =>
        _acl.IsAllowed(principal.Identity.Identifier, operation, resource.Domain, resource.Type, resource.Identifier);

    public Task<bool> PrincipalIsAuthorizedAsync(
        IDictionary<string, string> context,
        IPrincipal principal,
        string operation,
        NounQualifier resource,
        string purpose = "") =>
        _acl.IsAllowedAsync(principal.Identity.Identifier, operation, resource.Domain, resource.Type, resource.Identifier);
}
```

---

### 4. Register Services (ASP.NET Core)

```csharp
// Program.cs
builder.Services
    .AddHttpContextAccessor()
    .AddOAuth20IdentityAccessor()              // IPrincipalIdentityAccessor — reads JWT claims from HttpContext
    .AddSingleton<IPrincipalRoleProvider>(sp =>
        new MyRoleProvider(sp.GetRequiredService<IDataStore>()))
    .AddSingleton<IAuthorizationProvider>(sp =>
        new MyAuthorizationProvider(sp.GetRequiredService<IPrincipalRoleProvider>()))
    .AddSecurityManager("MyApplicationName"); // registers ISecurityManager as singleton
```

`AddSecurityManager` resolves `IPrincipalIdentityAccessor`, `IPrincipalRoleProvider`, and `IAuthorizationProvider` from the container if not passed directly. The `applicationName` string is stored on the security manager and can be used by authorization providers as a scope or purpose discriminator.

`AddOAuth20IdentityAccessor` maps standard OAuth2/OIDC JWT claims (`sub`, `client_id`, `iss`, `aud`, `scope`, `amr`, etc.) to attributes on `IPrincipalIdentity`. The `sub` claim becomes both `Identity.Identifier` and `Identity.Name`.

---

### 5. Use `ISecurityManager` in Application Logic

Inject `ISecurityManager` into your service. Call `CurrentPrincipal.IsAuthorizedAsync` before performing a sensitive operation. A common pattern is a shared helper that throws on failure:

```csharp
public class OrdersService
{
    private readonly ISecurityManager _security;
    private readonly IOrderRepository _repo;

    public OrdersService(ISecurityManager security, IOrderRepository repo)
    {
        _security = security;
        _repo = repo;
    }

    private async Task AuthorizeAsync(string operation, NounQualifier resource)
    {
        bool allowed = await _security.CurrentPrincipal
            .IsAuthorizedAsync(new Dictionary<string, string>(), operation, resource)
            .ConfigureAwait(false);

        if (!allowed)
            throw new NotAuthorized();
    }

    public async Task<string> CreateOrderAsync(OrderData data)
    {
        await AuthorizeAsync("CREATE", new NounQualifier { Domain = data.TenantId, Type = "ORDER" });
        return await _repo.InsertAsync(data).ConfigureAwait(false);
    }

    public async Task DeleteOrderAsync(string id)
    {
        OrderData existing = await _repo.GetAsync(id).ConfigureAwait(false);

        await AuthorizeAsync("DELETE", new NounQualifier
        {
            Domain = existing.TenantId,
            Type   = "ORDER",
            Identifier = id
        });

        await _repo.DeleteAsync(id).ConfigureAwait(false);
    }
}
```

---

### 6. Resource Identification with `NounQualifier`

`NounQualifier` identifies the resource being acted upon.

```csharp
// Wildcard — matches any resource of this type in the domain (used for LIST / CREATE)
new NounQualifier { Domain = tenantId, Type = "ORDER" }

// Specific resource — used for GET / UPDATE / DELETE
new NounQualifier { Domain = tenantId, Type = "ORDER", Identifier = orderId }

// No domain — applies globally (single-tenant scenarios)
new NounQualifier { Type = "CONFIGURATION" }
```

---

### 7. Handle `NotAuthorized` in Controllers / Endpoints

**MVC controllers:**

```csharp
[HttpDelete("{id}")]
[Authorize]
public async Task<IActionResult> DeleteOrder(string id)
{
    try
    {
        await _orders.DeleteOrderAsync(id).ConfigureAwait(false);
        return NoContent();
    }
    catch (NotAuthorized)
    {
        return Forbid();
    }
}
```

**Minimal API endpoints:**

```csharp
app.MapDelete("/orders/{id}", async (string id, OrdersService orders) =>
{
    try
    {
        await orders.DeleteOrderAsync(id).ConfigureAwait(false);
        return Results.NoContent();
    }
    catch (NotAuthorized)
    {
        return Results.Forbid();
    }
});
```

---

### 8. Testing

Mock `ISecurityManager` so unit tests are not coupled to infrastructure. The pattern below creates a stub that approves every authorization check:

```csharp
internal static ISecurityManager CreateAlwaysAuthorized(string principalId = "test-user")
{
    BasicPrincipalIdentity identity = new BasicPrincipalIdentity(
        principalId, principalId, isAuthenticated: true,
        authenticationType: "test", attributes: new Dictionary<string, string>());

    Mock<IPrincipal> mockPrincipal = new Mock<IPrincipal>();
    mockPrincipal.Setup(p => p.Identity).Returns(identity);
    mockPrincipal
        .Setup(p => p.IsAuthorizedAsync(
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<string>(),
            It.IsAny<NounQualifier>(),
            It.IsAny<string>()))
        .ReturnsAsync(true);

    Mock<ISecurityManager> mockManager = new Mock<ISecurityManager>();
    mockManager.Setup(m => m.CurrentPrincipal).Returns(mockPrincipal.Object);

    return mockManager.Object;
}
```

---

## Migration from Earlier Versions

Prior versions used a "user" model that has been superseded by the "principal" model. The old interfaces are still present but marked `[Obsolete]`.

| Deprecated | Replacement |
|---|---|
| `IUser` | `IPrincipal` |
| `IUserRoleProvider` | `IPrincipalRoleProvider` |
| `ISessionUserIdentityAccessor` | `IPrincipalIdentityAccessor` |
| `DomainObject` | `NounQualifier` |
| `ISecurityManager.CurrentUser` | `ISecurityManager.CurrentPrincipal` |
| `AddSecurityManager(..., IUserRoleProvider)` | `AddSecurityManager(..., IPrincipalRoleProvider)` |

`UserRoleProviderAdapter` is available internally to bridge existing `IUserRoleProvider` implementations to `IPrincipalRoleProvider` while migrating.
