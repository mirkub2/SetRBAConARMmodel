# SetRBAConARMmodel
Solution with simplified code for RBAC management of developer/IT teams accessing one common Azure subscription for projects/environments.
Code snippets in Program.cs solving this:
- create authorization tokens for Azure Resource Management API, Microsoft Graph API
- create resource group based on ARM template, start VM, stop VM, delete resource group
- get list of users in AAD associated with tenant and Azure Subscription throught REST. Translate users to simplified model "Display name, ObjectId" for simplier usage in application (e.g. in combobox ui elements) 
- get list of roles existing in resource group
- create role assignment for user from AAD and role from role list in resource group
- get list of role assignments in resource group
- delete role assignment from resource group

Actions can be called through parameters of console application. 
E.g. this is example, how to list all roles in resource group "rg_moj_projekt":
SetRBAConARMmodel listRBACroles "rg_moj_projekt" 

Don't forget to change identifications of your subscription in app.config:
- TenantID    
- SubscriptionID
- ApplicationID_ClientID
- ServiceCredential_ClientPassword

