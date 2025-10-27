Connect-AzAccount
$resourceGroup = "<RESOURCE_GROUP>"
$location = "<LOCATION>"
$keyVaultName = "<KEY_VAULT_NAME>"  # must be globally unique

New-AzResourceGroup -Name $resourceGroup -Location $location
New-AzKeyVault -Name $keyVaultName -ResourceGroupName $resourceGroup -Location $location
New-AzRoleAssignment -ObjectId "<OBJECT_ID>" -RoleDefinitionName "Key Vault Administrator" -Scope "/subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<RESOURCE_GROUP>/providers/Microsoft.KeyVault/vaults/<KEY_VAULT_NAME>"

# Create Storage Account and Function App
$storageName = "<STORAGE_ACCOUNT_NAME>"
$functionAppName = "<FUNCTION_APP_NAME>"  # must be globally unique

Set-AzContext -SubscriptionId "<SUBSCRIPTION_ID>"
New-AzStorageAccount -Name $storageName -Location $location -ResourceGroupName $resourceGroup -SkuName "Standard_LRS"

$resourceGroup = "<RESOURCE_GROUP>"
$storageName = "<STORAGE_ACCOUNT_NAME>"
(Get-AzStorageAccount -ResourceGroupName $resourceGroup -Name $storageName).Context.ConnectionString


New-AzFunctionApp -Name $functionAppName `
  -ResourceGroupName $resourceGroup `
  -StorageAccountName $storageName `
  -Location $location `
  -Runtime "dotnet-isolated" `
  -FunctionsVersion "4"

# Add Secrets from local.settings.json (make sure to grab function app uri)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "keywords" -SecretValue (ConvertTo-SecureString "<KEYWORDS>" -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "companyTokens" -SecretValue (ConvertTo-SecureString "<COMPANY_TOKENS>" -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "emailFrom" -SecretValue (ConvertTo-SecureString "<EMAIL_FROM>" -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "emailTo" -SecretValue (ConvertTo-SecureString "<EMAIL_TO>" -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "smtpHost" -SecretValue (ConvertTo-SecureString "<SMTP_HOST>" -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "smptPort" -SecretValue (ConvertTo-SecureString "<SMTP_PORT>" -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "smptUser" -SecretValue (ConvertTo-SecureString "<SMTP_USER>" -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "smtpPass" -SecretValue (ConvertTo-SecureString "<SMTP_PASS>" -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "connectionString" -SecretValue (ConvertTo-SecureString "<STORAGE_CONNECTION_STRING>" -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "AppInsightsInstrumentationKey" -SecretValue (ConvertTo-SecureString "<APPINSIGHTS_INSTRUMENTATIONKEY>" -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "AppInsightsConnectionString" -SecretValue (ConvertTo-SecureString "<APPINSIGHTS_CONNECTION_STRING>" -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "StorageAccountName" -SecretValue (ConvertTo-SecureString $storageName -AsPlainText -Force)
Set-AzKeyVaultSecret -VaultName $keyVaultName -Name "StorageAccountKey" -SecretValue (ConvertTo-SecureString "<STORAGE_ACCOUNT_KEY>" -AsPlainText -Force)

$vaultUri = "https://<YOUR-KEYVAULT-NAME>.vault.azure.net/"
# Assign Identity to Function App
$identity = Set-AzWebApp -ResourceGroupName $resourceGroup -Name $functionAppName -AssignIdentity $true -AppSetting @{
  "KEYWORDS" = "<KEYWORDS>"
  "COMPANY_TOKENS" = "<COMPANY_TOKENS>"
  "EMAIL_FROM" = "@Microsoft.KeyVault(SecretUri=$vaultUri/secrets/emailFrom)"
  "EMAIL_TO" = "@Microsoft.KeyVault(SecretUri=$vaultUri/secrets/emailTo)"
  "SMTP_HOST" = "@Microsoft.KeyVault(SecretUri=$vaultUri/secrets/smtpHost)"
  "SMTP_PORT" = "@Microsoft.KeyVault(SecretUri=$vaultUri/secrets/smptPort)"
  "SMTP_USER" = "@Microsoft.KeyVault(SecretUri=$vaultUri/secrets/smptUser)"
  "SMTP_PASS" = "@Microsoft.KeyVault(SecretUri=$vaultUri/secrets/smtpPass)"
  "AzureKeyVaultEndpoint" = "https://<YOUR-KEYVAULT-NAME>.vault.azure.net/"
  "AzureWebJobsStorage" = "@Microsoft.KeyVault(SecretUri=$vaultUri/secrets/connectionString)"
  "APPINSIGHTS_INSTRUMENTATIONKEY" = "@Microsoft.KeyVault(SecretUri=$vaultUri/secrets/AppInsightsInstrumentationKey)"
  "APPLICATIONINSIGHTS_CONNECTION_STRING" = "@Microsoft.KeyVault(SecretUri=$vaultUri/secrets/AppInsightsConnectionString)"
  "FUNCTIONS_WORKER_RUNTIME" = "dotnet-isolated"
  "FUNCTIONS_EXTENSION_VERSION" = "~4"
  "StorageAccountName" = "@Microsoft.KeyVault(SecretUri=$vaultUri/secrets/StorageAccountName)"
  "StorageAccountKey" = "@Microsoft.KeyVault(SecretUri=$vaultUri/secrets/StorageAccountKey)"
}
$principalId = $identity.Identity.PrincipalId
# Grant Access to Key Vault
New-AzRoleAssignment -ObjectId $principalId -RoleDefinitionName "Key Vault Secrets User" -Scope "/subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<RESOURCE_GROUP>/providers/Microsoft.KeyVault/vaults/<KEY_VAULT_NAME>"



