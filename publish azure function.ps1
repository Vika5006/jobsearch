# how to generate a zip file: dotnet publish -c Release -o ./publish, and then archive the publish folder/

Connect-AzAccount
$resourceGroup = "<RESOURCE_GROUP>"
$functionAppName = "<FUNCTION_APP_NAME>"  # must be globally unique

Compress-Archive -Path ./publish/* -DestinationPath ./function.zip

Publish-AzWebApp -ResourceGroupName $resourceGroup `
                 -Name $functionAppName `
                 -ArchivePath ./function.zip

