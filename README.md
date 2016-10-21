# SetRBAConARMmodel
Solution with simplified code for RBAC management of developer/IT teams accessing one common Azure subscription for projects/environments.
Code snippets in Program.cs solving this:
- create authorization tokens for Azure Resource Management API, Microsoft Graph API
- create resource group based on ARM template, start VM, stop VM, delete resource group
- get list of users in AAD associated with tenant and Azure Subscription throught REST. Translate users to simplified model "Display name, ObjectId" for simplier usage in appliacation (e.g. in combobox ui elements) 

