using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;
using Microsoft.Azure.Management.Compute;
using Microsoft.Rest;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;


//Application managing Azure resources needs application id registerred in Azure Active Directory. 
//Application ID can be generated in Azure Powershell with these commands
//....
//Login - AzureRmAccount
//get - azuresubscription
//$azureAdApplication = New-AzureRMADApplication -DisplayName "<Application name>" -HomePage "https://myaadapp.com" -IdentifierUris "https://myaadapp.com"  -Password "<SomepasswordforNewServicePrincipal123>"
//New-AzureRmADServicePrincipal -ApplicationId $azureAdApplication.ApplicationId
//New-AzureRMRoleAssignment -RoleDefinitionName Owner -ServicePrincipalName "https://myaadapp.com"
//....

//application will need access also to Microsoft Graph:
//  go to portal.azure.com -> Azure Active Directory -> App registrations -> select this app -> 
//   required permissions -> add -> select API -> Microsoft Graph -> go to Permissions ->
//    click on Application permissions + add also Read All users full profiles (Users.Read.All) as delegated -> 
//     Done to save

//application will need access also to Windows Azure Active Directory API -> 
//  go to portal.azure.com -> Azure Active Directory -> App registrations -> select this app -> 
//   required permissions -> add -> select API -> Windows Azure Active Directory API -> go to Permissions ->
//    add application permissions 'Read Directory data', delegated permission 'Sign in and read user profile'
//     Done to save

//application will authenticate against Microsoft Graph API using OAuth2 token.
// we have to allow this explicitly
//  go to portal management.windowsazure.com 
//    Active Directory -> Applications -> select this application from list -> 
//      click on Manifest button at bottom to open the inline manifest editor ->
//        search in manifest for the oauth2AllowImplicitFlow property. Change it to from false to true 
//          save manifest file and upload it back to azure portal.




namespace SetRBAConARMmodel
{
    class Program
    {

        /*configuration parameters per subscription, tenant in AAD and registration of this application in AAD application list*/
        static string TenantID = SetRBAConARMmodel.Properties.Settings.Default.TenantID;
        static string SubscriptionID = SetRBAConARMmodel.Properties.Settings.Default.SubscriptionID;
        static string ApplicationID_ClientID = SetRBAConARMmodel.Properties.Settings.Default.ApplicationID_ClientID;
        static string ServiceCredential_ClientPassword = SetRBAConARMmodel.Properties.Settings.Default.ServiceCredential_ClientPassword ;


        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("app needs at least on parameter, e.g. SetRBAConARMmodel listAADUsers");
                return;
            }
            //get authorization token for Azure Resource Management REST API and Azure Resource Management Library calls 
            var token = GetAuthorizationHeader();
            //get authorization Ouath2 token for Microsoft Graph API REST  calls 
            var tokenOauth2 = GetOAuth2Token();
            //create credential for Azure Resource Management Library calls  
            var credential = new Microsoft.Azure.TokenCloudCredentials(SubscriptionID , token);
            //create credential for Azure Resource Management REST API calls  
            var restcredential = new Microsoft.Rest.TokenCredentials(token);

            /***********************************************************************/
            /*action according to commandline arguments in this console application*/
            /***********************************************************************/

            //create resource group and azure service based od json ARM template and json ARM parameters
            //first generate ARM template files (e.g. from portal.azure.com) and save these files on Azure Storage
            //URL to these files will be parameters in command-line call
            if (args[0].Trim() == "createResourceGroupforTemplate")
            {
                if ( (args[1].Trim() != null) && (args[2].Trim() != null) && (args[3].Trim() != null))
                {
                    CreateResourceGroup(args[1].Trim(), args[2].Trim(),credential);
                    //e.g. template link https://<yourstragename>.blob.core.windows.net/<templates_container_name>/<json file name>
                    //e.g. parameter link  https://<yourstragename>.blob.core.windows.net/<templates_container_name>/<json parameters file name>
                    //e.g. deployment name Deployment1
                    CreateTemplateDeployment(args[2].Trim(), args[3].Trim(), args[4].Trim(), args[5].Trim(), credential);
                    Console.ReadLine();
                }
                else
                {
                    Console.WriteLine("creation of resource group with service according to template needs more parameter, e.g. SetRBAConARMmodel createResourceGroupforTemplate datacenter_location resource_group_name template_link parameter_link deployment_name");
                    Console.ReadLine();
                }
            }


            //start virtual machine in resource group
            if (args[0].Trim() == "startVM")
            {
                if ((args[1].Trim() != null) && (args[2].Trim() != null))
                {
                    StartVirtualMachineAsync(restcredential, args[1].Trim(), args[2].Trim(), SubscriptionID.Trim() );
                    Console.ReadLine();
                }
                else
                {
                    Console.WriteLine("starting VM in resource group needs more parameters, e.g. SetRBAConARMmodel startVM resource_group_name virtual_machine_name");
                    Console.ReadLine();
                }
            }

            //stop virtual machine in resource group
            if (args[0].Trim() == "stopVM")
            {
                if ((args[1].Trim() != null) && (args[2].Trim() != null))
                {
                    StopVirtualMachine(restcredential, args[1].Trim(), args[2].Trim(), SubscriptionID.Trim());
                    Console.ReadLine();
                }
                else
                {
                    Console.WriteLine("stopping VM in resource group needs more parameters, e.g. SetRBAConARMmodel stopVM resource_group_name virtual_machine_name");
                    Console.ReadLine();
                }
            }

            //list all users existing in AAD tenant attached to Azure subscription
            if (args[0].Trim() == "listAADUsers")
            {
                string uri = "https://graph.windows.net/" + TenantID.Trim() + "/users?api-version=1.6";
                var u = listAADUsers(uri, tokenOauth2);
                foreach (var item in u)
                {
                    Console.WriteLine(item.displayName + " has ObjectID: " + item.objectId);
                }
                Console.ReadLine();
            }

            //list all Azure resource group roles
            if (args[0].Trim() == "listRBACroles")
            {
                if (args[1].Trim() != null)
                {
                    string uri = "https://management.azure.com/subscriptions/" + SubscriptionID + "/resourceGroups/" + args[1].Trim() + "/providers/Microsoft.Authorization/roleDefinitions?api-version=2015-07-01";
                    var r = listResorceGroupRoles(uri, token);
                    foreach (var item in r)
                    {
                        Console.WriteLine("Role name " + item.roleName + " has id " + item.roleId);
                    }
                    Console.ReadLine();
                }
                else
                {
                    Console.WriteLine("listing of RBAC roles in resource group needs more parameters, e.g. SetRBAConARMmodel listRBACroles resource_group_name");
                    Console.ReadLine();
                }
            }


            //assign Azure resource group role to user existing in AAD tenant attached to Azure subscription
            if (args[0].Trim() == "addRBACroleAssignment")
            {
                if ((args[1].Trim() != null) && (args[2].Trim() != null) && (args[3].Trim() != null))
                { 
                    Guid g = Guid.NewGuid();
                    var resourceManagementClient = new ResourceManagementClient(credential);
                    var resourceGroup = resourceManagementClient.ResourceGroups.Get(args[1].Trim());
                    var resourceGroupID = resourceGroup.ResourceGroup.Id ;
                    
                    string uri = "https://management.azure.com/" + resourceGroupID.Trim() + "/providers/Microsoft.Authorization/roleAssignments/" + g.ToString().Trim() + "?api-version=2015-07-01";
                    string body = "{\"properties\": {\"roleDefinitionId\": \"" + resourceGroupID.Trim() + "/providers/Microsoft.Authorization/roleDefinitions/" + args[3].Trim() + "\", \"principalId\": \"" + args[2].Trim() + "\"}}";

                    var r = addRBACroleAssignment(uri, body, token);
                }
                else
               {
                Console.WriteLine("assignment of role to user from AAD to resource group needs more parameters, e.g. SetRBAConARMmodel addRBACroleAssignment resourceGroupId objectId_of_user roleId");
                    Console.ReadLine();
                }
            }


            //list all role assignments existing in resource group
            if (args[0].Trim() == "listRBACroleAssignments")
            {
                if (args[1].Trim() != null)
                {
                    string uri = "https://management.azure.com/subscriptions/" + SubscriptionID + "/resourceGroups/" + args[1].Trim() + "/providers/Microsoft.Authorization/roleAssignments?api-version=2015-07-01";
                    var ra = listResorceGroupRoleAssignments(uri, token);
                    foreach (var item in ra)
                    {
                        Console.WriteLine("Role Assignment ID " + item.roleAssignmentId + " means assignment of role id " + item.roleDefinitionId + " to user " + item.userId );
                    }
                    Console.ReadLine();
                }
                else
                {
                    Console.WriteLine("listing of RBAC role assignments in resource group needs more parameters, e.g. SetRBAConARMmodel listRBACroleAssignments resource_group_name");
                    Console.ReadLine();
                }
            }

            //delete role assignment existing in resource group
            if (args[0].Trim() == "deleteRBACroleAssignments")
            {
                if ((args[1].Trim() != null) && (args[2].Trim() != null))
                {
                    string uri = "https://management.azure.com/subscriptions/" + SubscriptionID + "/resourceGroups/" + args[1].Trim() + "/providers/Microsoft.Authorization/roleAssignments/" + args[2] + "?api-version=2015-07-01";
                            var r = deleteRBACRoleAssignment (uri, token);
                        Console.ReadLine();
                    }
                    else
                    {
                        Console.WriteLine("deletion of role assignment in resource group needs more parameters, e.g. SetRBAConARMmodel deleteRBACroleAssignments resource_group_name role_assignment_id");
                    Console.ReadLine();
                }
            }

            //completely delete resource group
            if (args[0].Trim() == "deleteResourceGroup")
            {
                if ((args[1].Trim() != null))
                {
                    DeleteResourceGroup(args[1].Trim(), credential);
                    Console.ReadLine();
                }
                else
                {
                    Console.WriteLine("deletion of resource group needs more parameters, e.g. SetRBAConARMmodel deleteResourceGroup resource_group_name");
                    Console.ReadLine();
                }
            }


        }

        private static string GetAuthorizationHeader()
        {
            ClientCredential cc = new ClientCredential(ApplicationID_ClientID.Trim() , ServiceCredential_ClientPassword.Trim());
            var context = new AuthenticationContext("https://login.windows.net/" + TenantID.Trim());
            //token will be for Windows Azure Management   
            var result = context.AcquireToken("https://management.azure.com/", cc);
            if (result == null)
            {
                throw new InvalidOperationException("Error in creating of authentication token");
            }
            string token = result.AccessToken;
            return token;
        }

        private static string GetOAuth2Token()
        {

            ClientCredential cc = new ClientCredential(ApplicationID_ClientID.Trim() , ServiceCredential_ClientPassword.Trim());
            var context = new AuthenticationContext("https://login.microsoftonline.com/" + TenantID.Trim() + "/oauth2/token");
            //token will be for Microsoft Graph API   
            var result = context.AcquireToken("https://graph.windows.net", cc);
            if (result == null)
            {
                throw new InvalidOperationException("Error in Ouath2 token generation");
            }
            string token = result.AccessToken;
            return token;
        }

        public static void CreateResourceGroup(string DatacenterLocation, string ResourceGroupName,TokenCloudCredentials credential)
        {
            Console.WriteLine("Creating resource group...");
            var resourceGroup = new ResourceGroup { Location = DatacenterLocation  };
            using (var resourceManagementClient = new ResourceManagementClient(credential))
            {
                var rgResult = resourceManagementClient.ResourceGroups.CreateOrUpdate(ResourceGroupName, resourceGroup);
                Console.WriteLine(rgResult.StatusCode.ToString() );
            }
        }

        public static void StopVirtualMachine(
          TokenCredentials credential,
          string groupName,
          string vmName,
          string subscriptionId)
        {
            Console.WriteLine("Stopping virtual machine...");
            var computeManagementClient = new ComputeManagementClient(credential)
            { SubscriptionId = subscriptionId };
            computeManagementClient.VirtualMachines.Deallocate(groupName, vmName);
        }

        public static async void StartVirtualMachineAsync(
          TokenCredentials credential,
          string groupName,
          string vmName,
          string subscriptionId)
        {
            Console.WriteLine("Starting virtual machine...");
            var computeManagementClient = new ComputeManagementClient(credential)
            { SubscriptionId = subscriptionId };
            await computeManagementClient.VirtualMachines.StartAsync(groupName, vmName);
        }
        public async static void CreateTemplateDeployment(string resourceGroupName,string templateLink, string parametersLlink, string deploymentName, TokenCloudCredentials credential)
        {
            Console.WriteLine("Alocating service according to deployment template...");
            var deployment = new Microsoft.Azure.Management.Resources.Models.Deployment();
            deployment.Properties = new DeploymentProperties
            {
                Mode = DeploymentMode.Incremental,
                TemplateLink = new TemplateLink
                {
                    Uri = new Uri( templateLink )
                },
                ParametersLink = new ParametersLink
                {
                    Uri = new Uri(parametersLlink )
                }
            };
            using (var templateDeploymentClient = new ResourceManagementClient(credential))
            {
                var dpResult = await templateDeploymentClient.Deployments.CreateOrUpdateAsync( resourceGroupName.Trim() , deploymentName.Trim() , deployment);
                Console.WriteLine(dpResult.StatusCode);
            }
        }
        public async static void DeleteResourceGroup(string resourceGroupName,TokenCloudCredentials credential)
        {
            using (var resourceGroupClient = new ResourceManagementClient(credential))
            {
                var rgResult = await resourceGroupClient.ResourceGroups.DeleteAsync(resourceGroupName);
                Console.WriteLine(rgResult.StatusCode);
            }
        }

        private static async Task<AuthenticationResult> GetAccessTokenAsync()
        {
            var cc = new ClientCredential( ApplicationID_ClientID.Trim()  , ServiceCredential_ClientPassword.Trim() );
            var context = new AuthenticationContext("https://login.windows.net/" + TenantID.Trim() );
            var token = await context.AcquireTokenAsync("https://management.azure.com/", cc);
            if (token == null)
            {
                throw new InvalidOperationException("Could not get the token");
            }
            return token;
        }

        private static IEnumerable<simplifiedUser> listAADUsers(string URI, String token)
        {
            Uri uri = new Uri(String.Format(URI));

            // Create the request
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.ContentLength = 0;
            httpWebRequest.Method = "GET";

            // Get the response
            HttpWebResponse httpResponse = null;
            try
            {
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error from : " + uri + ": " + ex.Message);
                return null;
            }

            string result = null;
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }

            var jsonUsers = JObject.Parse(result);
            var jsonUsersIn = jsonUsers.GetValue("value").Children();
            var users =
                from c in jsonUsersIn
                select new { name = c["displayName"].Value<string>(), objectID = c["objectId"].Value<string>() };

            //very valuable JSON LINQ info on http://www.newtonsoft.com/json/help/html/QueryingLINQtoJSON.htm

            List<simplifiedUser> userList = users.AsEnumerable().Select(item =>
            new simplifiedUser()
            {
              displayName = item.name ,
              objectId = item.objectID
            }).ToList();
            return userList ;
        }

        private static IEnumerable<simplifiedRole> listResorceGroupRoles(string URI, String token)
        {
            Uri uri = new Uri(String.Format(URI));

            // Create the request
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.ContentLength = 0;
            httpWebRequest.Method = "GET";

            // Get the response
            HttpWebResponse httpResponse = null;
            try
            {
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error from : " + uri + ": " + ex.Message);
                return null;
            }

            string result = null;
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }

            var jsonRoles = JObject.Parse(result);
            var jsonRolesIn = jsonRoles.GetValue("value").Children();

            var  jsonRolesIds =
             from p in jsonRolesIn
             select new { rolename = (string)p["properties"]["roleName"].Value<string>(), roleId = (string)p["name"].Value<string>() };

            List<simplifiedRole > roleList = jsonRolesIds.AsEnumerable().Select(item =>
               new simplifiedRole ()
               {
                   roleName = item.rolename ,
                   roleId = item.roleId 
               }).ToList();
            return roleList  ;
        }

        private static IEnumerable<simplifiedRoleAssignment> listResorceGroupRoleAssignments(string URI, String token)
        {
            Uri uri = new Uri(String.Format(URI));
            // Create the request
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.ContentLength = 0;
            httpWebRequest.Method = "GET";

            // Get the response
            HttpWebResponse httpResponse = null;
            try
            {
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error from : " + uri + ": " + ex.Message);
                return null;
            }

            string result = null;
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }

            var jsonRoleAssignments = JObject.Parse(result);
            var jsonRolesAssignmentsIn = jsonRoleAssignments.GetValue("value").Children();

            var jsonRoleAssignmentsIds =
             from p in jsonRolesAssignmentsIn
             select new { userId = (string)p["properties"]["principalId"].Value<string>(), roleDefinitionId = (string)p["properties"]["roleDefinitionId"].Value<string>(), roleAssignmentId = (string)p["name"].Value<string>() };

            List<simplifiedRoleAssignment> roleAssignmentsList = jsonRoleAssignmentsIds.AsEnumerable().Select(item =>
              new simplifiedRoleAssignment()
              {
                  userId = item.userId ,
                  roleDefinitionId = item.roleDefinitionId.Substring(item.roleDefinitionId.LastIndexOf("/")+1),
                  roleAssignmentId  = item.roleAssignmentId 
              }).ToList();
            return roleAssignmentsList;
        }

        private static string addRBACroleAssignment(string URI, string body, String token)
        {
            Uri uri = new Uri(String.Format(URI));
            // Create the request
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "PUT";

            try
            {
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(body);
                    streamWriter.Flush();
                    streamWriter.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error setting up stream writer: " + ex.Message);
            }

            // Get the response
            HttpWebResponse httpResponse = null;
            try
            {
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception ex)
            {
               Console.WriteLine ("Error from : " + uri + ": " + ex.Message);
                return null;
            }

            string result = null;
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            return result;
        }

        private static string deleteRBACRoleAssignment(string URI, String token)
        {
            Uri uri = new Uri(String.Format(URI));

            // Create the request
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "DELETE";

            // Get the response
            HttpWebResponse httpResponse = null;
            try
            {
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error from : " + uri + ": " + ex.Message);
                return null;
            }

            string result = null;
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            return result;
        }
    }
}
