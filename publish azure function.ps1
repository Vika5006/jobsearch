Connect-AzAccount
$resourceGroup = "<RESOURCE_GROUP>"
$functionAppName = "<FUNCTION_APP_NAME>"  # must be globally unique

Compress-Archive -Path ./publish/* -DestinationPath ./function.zip

Publish-AzWebApp -ResourceGroupName $resourceGroup `
                 -Name $functionAppName `
                 -ArchivePath ./function.zip

