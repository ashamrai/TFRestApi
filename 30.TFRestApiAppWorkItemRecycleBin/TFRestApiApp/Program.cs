using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Contracts;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Clients;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi;
using Microsoft.VisualStudio.Services.Identity.Client;

namespace TFRestApiApp
{
    class Program
    {


        static readonly string TFUrl = "https://dev.azure.com/<org>/";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

        #region Azure DevOps clients
        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static TeamHttpClient TeamClient;
        static ReleaseHttpClient ReleaseClient;
        static IdentityHttpClient IdentityClient;
        #endregion

        static void Main(string[] args)
        {
            try
            {
                string TeamProjectName = "<team_project_name>";
                int workItemID = 0;
                
                ConnectWithPAT(TFUrl, UserPAT);

                DeleteWorkItem(TeamProjectName, workItemID);
                ViewDeletedWorkItems(TeamProjectName);
                RestoreWorkItem(workItemID);
                //DestroyDeletedWorkItem(workItemID);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }


        /// <summary>
        /// Delete a work item to Recycle Bin
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="workItemID"></param>
        /// <param name="removePermanently"></param>
        static void DeleteWorkItem(string teamProjectName, int workItemID, bool removePermanently = false)
        {
            var deletedWI = WitClient.DeleteWorkItemAsync(teamProjectName, workItemID, removePermanently).Result;

            Console.WriteLine("Deleted work item:");

            Console.WriteLine("{0} : {1} : {2} : {3}", deletedWI.Project, deletedWI.Type, deletedWI.Id, deletedWI.Name);
            Console.WriteLine("Deleted by : {0} : {1}", deletedWI.DeletedBy, deletedWI.DeletedDate);
        }


        /// <summary>
        /// View Recycle Bin contents
        /// </summary>
        /// <param name="teamProjectName"></param>
        static void ViewDeletedWorkItems(string teamProjectName)
        {
            var deletedWIs = WitClient.GetDeletedWorkItemShallowReferencesAsync(teamProjectName).Result;

            if (deletedWIs.Count == 0) return;

            Console.WriteLine("Deleted work items:");

            foreach(var delWiRef in deletedWIs)
            {
                var deletedWI = WitClient.GetDeletedWorkItemAsync((int)delWiRef.Id).Result;

                Console.WriteLine("{0} | {1} | {2} | {3} | {4}", deletedWI.Type, deletedWI.Id, deletedWI.Name, deletedWI.DeletedBy, deletedWI.DeletedDate);
            }

        }

        /// <summary>
        /// Restore work item from Recycle Bin
        /// </summary>
        /// <param name="workItemID"></param>
        static void RestoreWorkItem(int workItemID)
        {

            var restoredWI = WitClient.RestoreWorkItemAsync(new WorkItemDeleteUpdate() { IsDeleted = false }, workItemID).Result;

            Console.WriteLine("Restored work item:");

            Console.WriteLine("{0} : {1} : {2} : {3}", restoredWI.Project, restoredWI.Type, restoredWI.Id, restoredWI.Name);
            Console.WriteLine("Deleted by : {0} : {1}", restoredWI.DeletedBy, restoredWI.DeletedDate);
        }

        /// <summary>
        /// Destroy a work item from Recycle Bin
        /// </summary>
        /// <param name="workItemID"></param>
        static void DestroyDeletedWorkItem(int workItemID)
        {
            WitClient.DestroyWorkItemAsync(workItemID).Wait();

            Console.WriteLine("Work Item {0} is destroyed", workItemID);
        }

        #region create new connections

        static void InitClients(VssConnection Connection)
        {
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            BuildClient = Connection.GetClient<BuildHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            TeamClient = Connection.GetClient<TeamHttpClient>();
            GitClient = Connection.GetClient<GitHttpClient>();
            TfvsClient = Connection.GetClient<TfvcHttpClient>();
            TestManagementClient = Connection.GetClient<TestManagementHttpClient>();
            ReleaseClient = Connection.GetClient<ReleaseHttpClient>();
            IdentityClient = Connection.GetClient<IdentityHttpClient>();
        }

        static void ConnectWithDefaultCreds(string ServiceURL)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new VssCredentials());
            InitClients(connection);
        }

        static void ConnectWithCustomCreds(string ServiceURL, string User, string Password)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new WindowsCredential(new NetworkCredential(User, Password)));
            InitClients(connection);
        }

        static void ConnectWithPAT(string ServiceURL, string PAT)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new VssBasicCredential(string.Empty, PAT));
            InitClients(connection);
        }
        #endregion
    }
}
