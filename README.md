# Identity Samples for Azure AD

## Introduction

This repository contains a Visual Studio (Code) solution that demonstrates modern claims-based identity scenarios for .NET developers, with a particular focus on authentication and authorization using [Azure Active Directory](https://docs.microsoft.com/en-us/azure/active-directory/).

The main purpose for this sample is to show end-to-end scenarios with minimal layers of abstraction, using the latest versions of the following technologies:

- The [**Microsoft identity platform (v2.0)**](https://docs.microsoft.com/en-us/azure/active-directory/develop/azure-ad-endpoint-comparison) instead of the Azure AD (v1.0) endpoint
- The [**Microsoft Graph API**](https://docs.microsoft.com/en-us/graph/use-the-api) instead of the Azure AD Graph API
- The [**Microsoft Authentication Library (MSAL)**](https://docs.microsoft.com/en-us/azure/active-directory/develop/msal-overview) instead of Active Directory Authentication Library (ADAL)

Although less important, the sample applications are also developed using [.NET Core](https://docs.microsoft.com/en-us/dotnet/core/) to take advantage of the latest versions of the relevant client libraries and server middleware.

For other samples, see [Microsoft identity platform code samples (v2.0 endpoint)](https://docs.microsoft.com/en-us/azure/active-directory/develop/sample-v2-code) and specifically the excellent [Tutorial - Enable your Web Apps to sign-in users and call APIs with the Microsoft identity platform for developers](https://github.com/Azure-Samples/active-directory-aspnetcore-webapp-openidconnect-v2).

**IMPORTANT NOTE: The code in this repository is _not_ production-ready. It serves only to demonstrate the main points via minimal working code, and contains no exception handling or other special cases. Refer to the official documentation and samples for more information. Similarly, by design, it does not implement any caching or data persistence (e.g. to a database) to minimize the concepts and technologies being used.**

## Scenario

This sample demonstrates an expense reporting solution which users can access through client applications that interact with a back-end API.

The core functionality is implemented in the **Expenses API**, which exposes a number of operations that are secured via OAuth 2.0 bearer tokens. A **client web application** signs the user in via OpenID Connect and then calls into the Expenses API to provide the user experience to create and approve expenses. There's also a **payout processor application** (a console app simulating a daemon service or background job) to mark approved expenses as paid out in the Expenses API, using the OAuth 2.0 Client Credentials flow. The Expenses API in turn calls into the **Microsoft Graph API** to get additional details of the signed-in user (to demonstrate a multi-tier application using the delegated OAuth 2.0 On-Behalf-Of flow).

## Permission Model

### Scope & Role Design

A well thought-out permission model is one of the more important design aspects of an API. The Expenses API uses a combination of [**OAuth 2.0 scopes**](https://docs.microsoft.com/en-us/azure/active-directory/develop/v2-permissions-and-consent#scopes-and-permissions) and [**Azure AD App Roles**](https://docs.microsoft.com/en-us/azure/architecture/multitenant-identity/app-roles#roles-using-azure-ad-app-roles) to secure its various operations.

Think of **scopes** as being something the user "owns" and the app wants (e.g. access to a user's mailbox or expense reports), which means that users need to _consent_ to letting an application use these permissions on their behalf.

**Roles** on the other hand are something the app (developer or administrator) grants to the user, not something the user "owns" or would want to refuse (e.g. being a contributor on an Azure resource group, a payroll administrator for the company's HR system, or an approver for expense reports).

Oversimplifying this a bit, you could say that **scopes are permissions that the app gets from the user**, whereas **roles are permissions that the user gets from the app**.

> **Note:** In Azure AD, application permissions (i.e. not delegated permissions on behalf of a user, but permissions representing what the application itself can do) are always modeled as _roles_, not _scopes_.

### Scopes

The Expenses API publishes the following scopes, which should be granular enough to cater for different client application use cases:

| Scope                | Who can consent  | Description                                                           |
| -------------------- | ---------------- | --------------------------------------------------------------------- |
| `Identity.Read`      | Admins and users | Allows the app to get information about the signed-in user's identity |
| `Expenses.Read`      | Admins and users | Allows the app to read the signed-in user's expenses                  |
| `Expenses.ReadWrite` | Admins and users | Allows the app to read and write the signed-in user's expenses        |
| `Expenses.Read.All`  | Admins only      | Allows the app to read expenses for all users                         |

The web application statically (i.e. in its [Azure AD app manifest](https://docs.microsoft.com/en-us/azure/active-directory/develop/reference-app-manifest)) requests access to only those scopes it really needs to perform its core use cases, i.e. the `Identity.Read`, `Expenses.Read` and `Expenses.ReadWrite` scopes. This means that users are required to consent to these scopes as soon as they sign in (or an admin can consent to these scopes on behalf of all the organization's users).

The Expenses API uses the OAuth 2.0 On-Behalf-Of flow to retrieve additional user information from the Microsoft Graph API, for which it needs access to its `User.Read` scope. Because the API's app manifest declares the web application to be a "[known client application](https://docs.microsoft.com/en-us/azure/active-directory/develop/reference-app-manifest#manifest-reference)", and the web application requests the special `/.default` scope at sign in, the user will perform a [combined consent](https://docs.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-on-behalf-of-flow#gaining-consent-for-the-middle-tier-application) for both the web application _and_ the API's statically requested scopes.

The web application also uses the dynamic consent capability of the Microsoft identity platform (v2.0) to acquire additional consent from the user when needed, e.g. when approving other people's expenses - which implies being able to read them in the first place via the `Expenses.Read.All` scope. This is declared as an admin-only scope, so that regular users cannot consent to it (because it exposes sensitive information, i.e. expense reports of other users).

### Roles

The Expenses API publishes the following roles, which can be assigned to users or applications:

| Role                     | Who can be assigned | Description                                      |
| ------------------------ | ------------------- | ------------------------------------------------ |
| `ExpenseSubmitter`       | Users               | Expense submitters can create and edit expenses  |
| `ExpenseApprover`        | Users               | Expense approvers can approve submitted expenses |
| `Expenses.ReadWrite.All` | Applications        | Read and write expenses for all users            |

The `ExpenseSubmitter` and `ExpenseApprover` roles are used to secure actions that users are allowed to perform.

The `Expenses.ReadWrite.All` role is used by the payout processor application which checks approved expenses and marks them as paid out. Since application permissions cannot be modeled as scopes, this sensitive permission is secured by the `Expenses.ReadWrite.All` role (which by definition requires admin consent, as application permissions can only be granted by admins). Modeling the dependencies between applications as roles in this way also makes it easier to discover which applications have which permissions in other applications.

> **Note:** In this sample, the client web application retrieves the user's roles from the Expenses API after sign-in. This means the user is not assigned any roles on the client application directly but only on the API. The roles are retrieved from the API merely to reflect the user's permissions properly in the user experience, e.g. to disable certain functionality that would fail anyway if a particular API operation were called from the client application. Even though the client application uses the user's roles as seen by the API to improve the user experience, the end responsibility around safeguarding operations is still always maintained in the API.

## Setup

To use these samples, run the "Setup.ps1" PowerShell script in the "Setup" folder. This creates all the application registrations in Azure AD, configures the applications to use the correct values for the app registrations (e.g. configures the Azure AD Tenant, Client ID, Client Secret, ...) and adds the user that registered them to the Azure AD App Roles exposed by the API.
