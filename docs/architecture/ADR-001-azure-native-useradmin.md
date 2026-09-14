# ADR-001: Azure-Native UserAdmin Target

## Status
Accepted

## Decision
UserAdmin will evolve from a Windows-hosted PowerShell runner toward an Azure-native Entra and Microsoft 365 application. The target application will use Azure App Service, Microsoft Entra authentication and application authorization, typed .NET application services, managed workload identities, Microsoft Graph where supported, and Azure-native observability.

Existing scripts, controllers, and views are migration evidence for user journeys, inputs, business rules, dependencies, and side effects. They are not the target execution architecture.

## Rejected Future-State Approaches
- IIS on CCCS923 or another Windows IIS server.
- An Azure VM hosting the application.
- A Hybrid Runbook Worker as permanent application architecture.
- Retaining the generic PowerShell runner as the core execution model.
- Preserving on-premises AD or Exchange PowerShell solely for compatibility.

## Consequences
- Business operations must be mapped independently to supported Graph, Entra, Exchange Online, or Azure platform capabilities.
- Authentication and operator authorization move from Windows-domain allow-list configuration to Entra identities and application roles.
- Managed identities or workload identities replace host/service-account privilege and stored deployment credentials.
- The generic Script List is a candidate for removal after typed replacements exist.
- New-user provisioning, licensing, mailbox, deletion, and credential outcomes require business and tenant decisions before implementation.
- GitHub Actions will evolve from packaging for IIS to Azure-native CI/CD using federated OIDC authentication.

## Guardrails
This decision does not authorize deployment, Azure resource provisioning, Entra changes, Graph calls, Exchange changes, or removal of legacy functionality. Each change is delivered as a separately reviewed increment.
