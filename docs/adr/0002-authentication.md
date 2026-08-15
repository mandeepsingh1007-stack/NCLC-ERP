# ADR-002: Authentication Strategy

- **Status**: Accepted
- **Date**: 2026-08-14
- **Context**: The platform needs authentication for multi-client/organization/role access.

## Decision
Use **ASP.NET Core Identity with JWT bearer tokens** as the default authentication mechanism. External IdP integration (OpenID Connect / SAML) is supported via middleware.

## Rationale
- ASP.NET Core Identity is built into the framework, requires no external dependencies.
- JWT bearer tokens are stateless and work well with the SPA architecture (React frontend).
- OpenID Connect middleware is available in ASP.NET Core for external IdP integration.
- SAML is a later concern — can be added when a specific business requirement exists.

## Alternatives Considered
- **External IdP only (OIDC)**: Better for enterprise SSO but adds external dependency. Can be added later.
- **API Keys**: Useful for service-to-service auth but not for user authentication. Will be added as a secondary mechanism.

## Consequences
- Session management is token-based (no server-side sessions).
- Token revocation requires a deny list / short TTL strategy.
- Multi-tenant auth (client/org isolation) is enforced server-side.
