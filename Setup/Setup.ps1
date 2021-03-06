################################################################################
# Set up some configuration values
################################################################################
$WebApiDisplayName = "Expenses.Api"
$WebAppDisplayName = "Expenses.Client.WebApp"
$WebSpaDisplayName = "Expenses.Client.WebSpa"
$PayoutProcessorDisplayName = "Expenses.Client.PayoutProcessor"

################################################################################
# Initialize
################################################################################
$ErrorActionPreference = "Stop" # Break on errors
$ApplicationDisplayNames = @($WebApiDisplayName, $WebAppDisplayName, $WebSpaDisplayName, $PayoutProcessorDisplayName)
$CredentialStartDate = (Get-Date).AddDays(-1) # Make sure the start date is in the past
$CredentialEndDate = $CredentialStartDate.AddYears(2)

################################################################################
# Functions
################################################################################

function Remove-Oauth2Permission($ObjectId, $Oauth2PermissionName)
{
    # Retrieve the application registration and find the permission by name.
    $AppRegistration = Get-AzureADApplication -ObjectId $ObjectId
    $UserImpersonationPermission = $AppRegistration.Oauth2Permissions | where Value -eq $Oauth2PermissionName

    # Disable the permission first (otherwise it cannot be deleted).
    $UserImpersonationPermission.IsEnabled = $False
    Set-AzureADApplication -ObjectId $AppRegistration.ObjectId -Oauth2Permissions $AppRegistration.Oauth2Permissions

    # Remove the permission.
    $IsRemoved = $AppRegistration.Oauth2Permissions.Remove($UserImpersonationPermission)
    Set-AzureADApplication -ObjectId $AppRegistration.ObjectId -Oauth2Permissions $AppRegistration.Oauth2Permissions
}

################################################################################
# Sign in if needed
################################################################################
try
{
    $CurrentSessionInfo = Get-AzureADCurrentSessionInfo
}
catch [Exception]
{
    Write-Warning "Authorizing access to Azure Active Directory. Please log in with an admin account of the directory itself (not an external account)!"
    Connect-AzureAD
    $CurrentSessionInfo = Get-AzureADCurrentSessionInfo
}

$CurrentUser = Get-AzureADUser -ObjectId $CurrentSessionInfo.Account

################################################################################
# Start from scratch by removing existing app registrations for the same apps
################################################################################
$ExistingApplications = Get-AzureADApplication
$ExistingApplications | Where-Object { $ApplicationDisplayNames.Contains($_.DisplayName) } | ForEach-Object {
    Write-Host "Deleting Azure AD application ""$($_.DisplayName)""..."
    Remove-AzureADApplication -ObjectId $_.ObjectId
}

################################################################################
# Register the Web API
################################################################################
Write-Host "Registering Azure AD application ""$WebApiDisplayName""..."
$WebApiRegistration = New-AzureADApplication `
    -DisplayName $WebApiDisplayName `
    -Oauth2AllowImplicitFlow $False `
    -GroupMembershipClaims "All" <# To demonstrate that you can also get group memberships in the token #> `
    -IdentifierUris @("api://expenses") `
    -AppRoles @( `
        [Microsoft.Open.AzureAD.Model.AppRole]@{ `
            AllowedMemberTypes = @("Application"); `
            Description = "Read and write expenses for all users"; `
            DisplayName = "Expenses.ReadWrite.All"; `
            Id = [Guid]::NewGuid(); `
            Value = "Expenses.ReadWrite.All"; `
        }, `
        [Microsoft.Open.AzureAD.Model.AppRole]@{ `
            AllowedMemberTypes = @("User"); `
            Description = "Expense approvers can approve submitted expenses"; `
            DisplayName = "Expense Approver"; `
            Id = [Guid]::NewGuid(); `
            Value = "ExpenseApprover"; `
        }, `
        [Microsoft.Open.AzureAD.Model.AppRole]@{ `
            AllowedMemberTypes = @("User"); `
            Description = "Expense submitters can create and edit expenses"; `
            DisplayName = "Expense Submitter"; `
            Id = [Guid]::NewGuid(); `
            Value = "ExpenseSubmitter"; `
        } `
    ) `
    -Oauth2Permissions @( `
        [Microsoft.Open.AzureAD.Model.Oauth2Permission]@{ `
            AdminConsentDescription = "Allows the app to read expenses for all users."; `
            AdminConsentDisplayName = "Read expenses for all users"; `
            Id = [Guid]::NewGuid(); `
            "Type" = "Admin"; `
            UserConsentDescription = "Allows the app to read expenses for all users."; `
            UserConsentDisplayName = "Read expenses for all users"; `
            Value = "Expenses.Read.All"; `
        }, `
        [Microsoft.Open.AzureAD.Model.Oauth2Permission]@{ `
            AdminConsentDescription = "Allows the app to get information about the signed-in user's identity"; `
            AdminConsentDisplayName = "Read user identity"; `
            Id = [Guid]::NewGuid(); `
            "Type" = "User"; `
            UserConsentDescription = "Allows the app to get information about your identity."; `
            UserConsentDisplayName = "Read your identity"; `
            Value = "Identity.Read"; `
        }, `
        [Microsoft.Open.AzureAD.Model.Oauth2Permission]@{ `
            AdminConsentDescription = "Allows the app to read and write the signed-in user's expenses."; `
            AdminConsentDisplayName = "Read and write user expenses"; `
            Id = [Guid]::NewGuid(); `
            "Type" = "User"; `
            UserConsentDescription = "Allows the app to read and write your expenses."; `
            UserConsentDisplayName = "Read and write your expenses"; `
            Value = "Expenses.ReadWrite"; `
        }, `
        [Microsoft.Open.AzureAD.Model.Oauth2Permission]@{ `
            AdminConsentDescription = "Allows the app to read the signed-in user's expenses."; `
            AdminConsentDisplayName = "Read user expenses"; `
            Id = [Guid]::NewGuid(); `
            "Type" = "User"; `
            UserConsentDescription = "Allows the app to read your expenses."; `
            UserConsentDisplayName = "Read your expenses"; `
            Value = "Expenses.Read"; `
        } `
    ) `
    -RequiredResourceAccess @( <# Define access to other applications #> `
        [Microsoft.Open.AzureAD.Model.RequiredResourceAccess]@{ `
            ResourceAppId = "00000003-0000-0000-c000-000000000000"; <# Access the Microsoft Graph #> `
            ResourceAccess = [Microsoft.Open.AzureAD.Model.ResourceAccess]@{ `
                Id = "e1fe6dd8-ba31-4d61-89e7-88639da4683d"; <# Request permission "User.Read": Sign you in and read your profile #> `
                Type = "Scope" `
            } `
        } `
    )

# Add a password credential (client secret) to the Application
$WebApiClientSecret = New-AzureADApplicationPasswordCredential -ObjectId $WebApiRegistration.ObjectId -CustomKeyIdentifier "ClientSecret" -StartDate $CredentialStartDate -EndDate $CredentialEndDate
$WebApiClientSecretValue = $WebApiClientSecret.Value

# Associate a Service Principal to the Application 
$WebApiServicePrincipal = New-AzureADServicePrincipal -AppId $WebApiRegistration.AppId

# Set the owner of the Application to the current user
$WebApiRegistrationOwner = Add-AzureADApplicationOwner -ObjectId $WebApiRegistration.ObjectId -RefObjectId $CurrentUser.ObjectId

# Disable and then remove the default generated "user_impersonation" scope
Remove-Oauth2Permission -ObjectId $WebApiRegistration.ObjectId -Oauth2PermissionName "user_impersonation"

# Upload the logo
Set-AzureADApplicationLogo -ObjectId $WebApiRegistration.ObjectId -FilePath "$PSScriptRoot\Logo-Expenses.Api.png"

################################################################################
# Register the Web App
################################################################################
Write-Host "Registering Azure AD application ""$WebAppDisplayName""..."
$WebAppRegistration = New-AzureADApplication `
    -DisplayName $WebAppDisplayName `
    -Oauth2AllowImplicitFlow $True `
    -GroupMembershipClaims "All" <# To demonstrate that you can also get group memberships in the token #> `
    -Homepage "https://localhost:5003/" `
    -ReplyUrls $("https://localhost:5003/signin-oidc") `
    -LogoutUrl "https://localhost:5003/signout-oidc" `
    -RequiredResourceAccess @( <# Define access to other applications #> `
        [Microsoft.Open.AzureAD.Model.RequiredResourceAccess]@{ `
            ResourceAppId = "00000003-0000-0000-c000-000000000000"; <# Access the Microsoft Graph #> `
            ResourceAccess = [Microsoft.Open.AzureAD.Model.ResourceAccess]@{ `
                Id = "e1fe6dd8-ba31-4d61-89e7-88639da4683d"; <# Request permission "User.Read": Sign you in and read your profile #> `
                Type = "Scope" `
            } `
        }, `
        [Microsoft.Open.AzureAD.Model.RequiredResourceAccess]@{ `
            ResourceAppId = $WebApiRegistration.AppId; <# Access certain scopes from the Expenses API by default (to grant initial static consent to them) #> `
            ResourceAccess = @(
                [Microsoft.Open.AzureAD.Model.ResourceAccess]@{ `
                    Id = ($WebApiRegistration.Oauth2Permissions | where Value -eq "Expenses.Read").Id; `
                    Type = "Scope" `
                }, `
                [Microsoft.Open.AzureAD.Model.ResourceAccess]@{ `
                    Id = ($WebApiRegistration.Oauth2Permissions | where Value -eq "Expenses.ReadWrite").Id; `
                    Type = "Scope" `
                }, `
                [Microsoft.Open.AzureAD.Model.ResourceAccess]@{ `
                    Id = ($WebApiRegistration.Oauth2Permissions | where Value -eq "Identity.Read").Id; `
                    Type = "Scope" `
                } `
            ) `
        } `
    )

# Add a password credential (client secret) to the Application
$WebAppClientSecret = New-AzureADApplicationPasswordCredential -ObjectId $WebAppRegistration.ObjectId -CustomKeyIdentifier "ClientSecret" -StartDate $CredentialStartDate -EndDate $CredentialEndDate
$WebAppClientSecretValue = $WebAppClientSecret.Value

# Associate a Service Principal to the Application 
$WebAppServicePrincipal = New-AzureADServicePrincipal -AppId $WebAppRegistration.AppId
    
# Set the owner of the Application to the current user
$WebAppRegistrationOwner = Add-AzureADApplicationOwner -ObjectId $WebAppRegistration.ObjectId -RefObjectId $CurrentUser.ObjectId

# Disable and then remove the default generated "user_impersonation" scope
Remove-Oauth2Permission -ObjectId $WebAppRegistration.ObjectId -Oauth2PermissionName "user_impersonation"

# Upload the logo
Set-AzureADApplicationLogo -ObjectId $WebAppRegistration.ObjectId -FilePath "$PSScriptRoot\Logo-Expenses.Client.WebApp.png"

################################################################################
# Register the Web SPA
################################################################################
Write-Host "Registering Azure AD application ""$WebSpaDisplayName""..."
$WebSpaRegistration = New-AzureADApplication `
    -DisplayName $WebSpaDisplayName `
    -Oauth2AllowImplicitFlow $True `
    -GroupMembershipClaims "All" <# To demonstrate that you can also get group memberships in the token #> `
    -Homepage "https://localhost:5005/" `
    -ReplyUrls $("https://localhost:5005") `
    -LogoutUrl "https://localhost:5003" `
    -RequiredResourceAccess @( <# Define access to other applications #> `
        [Microsoft.Open.AzureAD.Model.RequiredResourceAccess]@{ `
            ResourceAppId = "00000003-0000-0000-c000-000000000000"; <# Access the Microsoft Graph #> `
            ResourceAccess = [Microsoft.Open.AzureAD.Model.ResourceAccess]@{ `
                Id = "e1fe6dd8-ba31-4d61-89e7-88639da4683d"; <# Request permission "User.Read": Sign you in and read your profile #> `
                Type = "Scope" `
            } `
        }, `
        [Microsoft.Open.AzureAD.Model.RequiredResourceAccess]@{ `
            ResourceAppId = $WebApiRegistration.AppId; <# Access certain scopes from the Expenses API by default (to grant initial static consent to them) #> `
            ResourceAccess = @(
                [Microsoft.Open.AzureAD.Model.ResourceAccess]@{ `
                    Id = ($WebApiRegistration.Oauth2Permissions | where Value -eq "Expenses.Read").Id; `
                    Type = "Scope" `
                }, `
                [Microsoft.Open.AzureAD.Model.ResourceAccess]@{ `
                    Id = ($WebApiRegistration.Oauth2Permissions | where Value -eq "Expenses.ReadWrite").Id; `
                    Type = "Scope" `
                }, `
                [Microsoft.Open.AzureAD.Model.ResourceAccess]@{ `
                    Id = ($WebApiRegistration.Oauth2Permissions | where Value -eq "Identity.Read").Id; `
                    Type = "Scope" `
                } `
            ) `
        } `
    )

# Associate a Service Principal to the Application 
$WebSpaServicePrincipal = New-AzureADServicePrincipal -AppId $WebSpaRegistration.AppId
    
# Set the owner of the Application to the current user
$WebSpaRegistrationOwner = Add-AzureADApplicationOwner -ObjectId $WebSpaRegistration.ObjectId -RefObjectId $CurrentUser.ObjectId

# Disable and then remove the default generated "user_impersonation" scope
Remove-Oauth2Permission -ObjectId $WebSpaRegistration.ObjectId -Oauth2PermissionName "user_impersonation"

# Upload the logo
Set-AzureADApplicationLogo -ObjectId $WebSpaRegistration.ObjectId -FilePath "$PSScriptRoot\Logo-Expenses.Client.WebApp.png"

# TODO: Update the app manifest to change the Reply URL to "type": "Spa" (see https://docs.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-auth-code-flow#setup-required-for-single-page-apps)

################################################################################
# Register the Payout Processor
################################################################################
Write-Host "Registering Azure AD application ""$PayoutProcessorDisplayName""..."
$PayoutProcessorRegistration = New-AzureADApplication `
    -DisplayName $PayoutProcessorDisplayName `
    -RequiredResourceAccess @( <# Define access to other applications #> `
        [Microsoft.Open.AzureAD.Model.RequiredResourceAccess]@{ `
            ResourceAppId = $WebApiRegistration.AppId; <# Access certain scopes from the Expenses API by default (to grant initial static consent to them) #> `
            ResourceAccess = [Microsoft.Open.AzureAD.Model.ResourceAccess]@{ `
                Id = ($WebApiRegistration.AppRoles | where Value -eq "Expenses.ReadWrite.All").Id; `
                Type = "Role" `
            } `
        } `
    )


# Add a password credential (client secret) to the Application
$PayoutProcessorClientSecretCredential = New-AzureADApplicationPasswordCredential -ObjectId $PayoutProcessorRegistration.ObjectId -CustomKeyIdentifier "ClientSecret" -StartDate $CredentialStartDate -EndDate $CredentialEndDate
$PayoutProcessorClientSecretValue = $PayoutProcessorClientSecretCredential.Value

# Add a client certificate credential to the Application
$PayoutProcessorClientCertificateName = "ExpensePayoutProcessor"
$PayoutProcessorClientCertificate = Get-ChildItem Cert:\CurrentUser\My | where { $_.Subject -eq "CN=$PayoutProcessorClientCertificateName" }
if ($PayoutProcessorClientCertificate -eq $null)
{
    $PayoutProcessorClientCertificate = New-SelfSignedCertificate -CertStoreLocation Cert:\CurrentUser\My -Subject $PayoutProcessorClientCertificateName -KeyExportPolicy Exportable -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider" -NotBefore $CredentialStartDate.AddDays(-1) -NotAfter $CredentialEndDate.AddDays(1) # Ensure the certificate validity period is greater than the credential validity period 
}
$PayoutProcessorClientCertificateBase64Thumbprint = [System.Convert]::ToBase64String($PayoutProcessorClientCertificate.GetCertHash())
$PayoutProcessorClientCertificateBase64Value = [System.Convert]::ToBase64String($PayoutProcessorClientCertificate.GetRawCertData())
$PayoutProcessorClientCertificateCredential = New-AzureADApplicationKeyCredential -ObjectId $PayoutProcessorRegistration.ObjectId -CustomKeyIdentifier $PayoutProcessorClientCertificateBase64Thumbprint -Type AsymmetricX509Cert -Usage Verify -Value $PayoutProcessorClientCertificateBase64Value -StartDate $CredentialStartDate -EndDate $CredentialEndDate

# Associate a Service Principal to the Application 
$PayoutProcessorServicePrincipal = New-AzureADServicePrincipal -AppId $PayoutProcessorRegistration.AppId
    
# Set the owner of the Application to the current user
$PayoutProcessorRegistrationOwner = Add-AzureADApplicationOwner -ObjectId $PayoutProcessorRegistration.ObjectId -RefObjectId $CurrentUser.ObjectId

# Disable and then remove the default generated "user_impersonation" scope
Remove-Oauth2Permission -ObjectId $PayoutProcessorRegistration.ObjectId -Oauth2PermissionName "user_impersonation"

$PayoutProcessorRoleAssignment = New-AzureADServiceAppRoleAssignment -ObjectId $PayoutProcessorServicePrincipal.ObjectId -PrincipalId $PayoutProcessorServicePrincipal.ObjectId -ResourceId $WebApiServicePrincipal.ObjectId -Id ($WebApiRegistration.AppRoles | where Value -eq "Expenses.ReadWrite.All").Id

################################################################################
# Update Web API configuration
################################################################################

# Add the Client ID's to the Web API's "known client applications" list
Set-AzureADApplication -ObjectId $WebApiRegistration.ObjectId -KnownClientApplications @($WebAppRegistration.AppId, $WebSpaRegistration.AppId)

################################################################################
# Update local application configuration
################################################################################

Write-Host "Writing application configuration for ""$WebApiDisplayName""..."
$WebApiProjectDirectory = "$PSScriptRoot\..\Expenses.Api"
dotnet user-secrets --project $WebApiProjectDirectory set AzureAd:TenantId $CurrentSessionInfo.TenantId
dotnet user-secrets --project $WebApiProjectDirectory set AzureAd:Domain $CurrentSessionInfo.TenantDomain
dotnet user-secrets --project $WebApiProjectDirectory set AzureAd:ClientSecret "$WebApiClientSecretValue"
dotnet user-secrets --project $WebApiProjectDirectory set AzureAd:ClientId $WebApiRegistration.AppId

Write-Host "Writing application configuration for ""$WebAppDisplayName""..."
$WebAppProjectDirectory = "$PSScriptRoot\..\Expenses.Client.WebApp"
dotnet user-secrets --project $WebAppProjectDirectory set AzureAd:TenantId $CurrentSessionInfo.TenantId
dotnet user-secrets --project $WebAppProjectDirectory set AzureAd:Domain $CurrentSessionInfo.TenantDomain
dotnet user-secrets --project $WebAppProjectDirectory set AzureAd:ClientSecret "$WebAppClientSecretValue"
dotnet user-secrets --project $WebAppProjectDirectory set AzureAd:ClientId $WebAppRegistration.AppId

Write-Host "Writing application configuration for ""$WebSpaDisplayName""..."
$WebSpaProjectDirectory = "$PSScriptRoot\..\Expenses.Client.WebSpa"
Set-Content -Path "$WebSpaProjectDirectory\wwwroot\appsettings.js" -Value @"
const appsettings = {
  scopes: [
      "api://expenses/.default"
  ],
  aadClientId: "$($WebSpaRegistration.AppId)",
  aadAuthority: "https://login.microsoftonline.com/$($CurrentSessionInfo.TenantDomain)",
  expensesApiBaseUrl: "https://localhost:5001/"
};
"@

Write-Host "Writing application configuration for ""$PayoutProcessorDisplayName""..."
$PayoutProcessorProjectDirectory = "$PSScriptRoot\..\Expenses.Client.PayoutProcessor"
dotnet user-secrets --project $PayoutProcessorProjectDirectory set AzureAd:TenantId $CurrentSessionInfo.TenantId
dotnet user-secrets --project $PayoutProcessorProjectDirectory set AzureAd:Domain $CurrentSessionInfo.TenantDomain
dotnet user-secrets --project $PayoutProcessorProjectDirectory set AzureAd:ClientSecret "$PayoutProcessorClientSecretValue"
dotnet user-secrets --project $PayoutProcessorProjectDirectory set AzureAd:ClientId $PayoutProcessorRegistration.AppId
dotnet user-secrets --project $PayoutProcessorProjectDirectory set AzureAd:ClientCertificateName "$PayoutProcessorClientCertificateName"

################################################################################
# Assign App Roles to the current user
################################################################################

$ExpenseSubmitterRoleAssignment = New-AzureADUserAppRoleAssignment -ObjectId $CurrentUser.ObjectId -PrincipalId $CurrentUser.ObjectId -ResourceId $WebApiServicePrincipal.ObjectId -Id ($WebApiRegistration.AppRoles | where Value -eq "ExpenseSubmitter").Id
$ExpenseApproverRoleAssignment = New-AzureADUserAppRoleAssignment -ObjectId $CurrentUser.ObjectId -PrincipalId $CurrentUser.ObjectId -ResourceId $WebApiServicePrincipal.ObjectId -Id ($WebApiRegistration.AppRoles | where Value -eq "ExpenseApprover").Id