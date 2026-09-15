# ADR-001: Tactical UserAdmin and Strategic Azure-Native Successor

## Status
Accepted

## Decision
UserAdmin will use CCCS923/Nutanix as its approved tactical deployment target for the current hybrid identity operating model. The current process remains dependent on the hybrid identity and Exchange model, including CCCS653 as the Exchange Hybrid server, and must be completed, secured, deployed, and supported safely during its expected short lifetime.

Azure-native Entra/Microsoft 365 remains the approved strategic successor direction when the organisation's identity and infrastructure architecture permits it. The successor should replace the legacy execution model with typed business operations rather than lift-and-shift the same generic PowerShell architecture.

Existing scripts, controllers, and views are migration evidence for user journeys, inputs, business rules, dependencies, and side effects. They are not the target execution architecture.

## Current Tactical Platform
- CCCS923/Nutanix VPC is an intentional transitional platform and is not rejected as the current deployment target.
- CCCS653 and the hybrid Exchange dependency are current dependencies to understand, secure, and support during the tactical lifetime.
- A replacement IIS VM should not automatically become the long-term successor; it would extend the transitional model without resolving the strategic identity direction.

## Strategic Direction and Rejected Long-Term Approaches
- When Entra cloud-only identity becomes viable, prefer replacing the legacy execution model with typed Entra/M365 operations.
- Do not make CCCS923, another IIS server, or an Azure VM the long-term successor architecture by default.
- Do not make a Hybrid Runbook Worker the permanent strategic architecture.
- Do not retain the generic PowerShell runner as the strategic core execution model.
- Do not preserve on-premises AD or Exchange PowerShell after the relevant hybrid dependencies have been retired, unless a separately approved business dependency remains.

## Consequences
- Business operations must be mapped independently to supported Graph, Entra, Exchange Online, or Azure platform capabilities.
- Tactical work secures the current Windows/hybrid process, validates its dependencies, and supports controlled CCCS923 deployment and UAT.
- Strategic work moves authentication and operator authorization from Windows-domain allow-list configuration to Entra identities and application roles.
- Strategic managed identities or workload identities replace host/service-account privilege and stored deployment credentials.
- The generic Script List is restricted and hardened tactically, then is a candidate for removal after typed replacements exist.
- New-user provisioning, licensing, mailbox, deletion, and credential outcomes require business and tenant decisions in both horizons.
- GitHub Actions can continue validating and packaging the tactical application; strategic Azure-native CI/CD using federated OIDC is a later successor concern.

## Guardrails
This decision does not authorize deployment, Azure resource provisioning, Entra changes, Graph calls, Exchange changes, or removal of legacy functionality. Tactical deployment to CCCS923 remains separately approved and must support the current hybrid model. Each tactical or strategic change is delivered as a separately reviewed increment.
