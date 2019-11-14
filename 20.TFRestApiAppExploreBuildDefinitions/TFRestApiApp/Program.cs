using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    class Program
    {
        static readonly string TFUrl = "https://dev.azure.com/<org>/";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            try
            {
                string TeamProjectName = "<Team Project Name>";

                ConnectWithPAT(TFUrl, UserPAT);

                ListBuildDefinitions(TeamProjectName);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Show build definitions and builds in Team Project
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void ListBuildDefinitions(string TeamProjectName)
        {
            List<BuildDefinitionReference> buildDefs = BuildClient.GetDefinitionsAsync(TeamProjectName).Result;

            foreach(BuildDefinitionReference buildDef in buildDefs)
            {
                Console.WriteLine("+================BUILD DEFINITION=======================================================");
                Console.WriteLine(" ID:{0, -9}|NAME:{1, -35}|PATH:{2}", buildDef.Id, buildDef.Name, buildDef.Path);
                Console.WriteLine(" REV:{0, -8}|QUEUE:{1, -34}|QUEUE STATUS:{2}", buildDef.Revision, (buildDef.Queue != null) ? buildDef.Queue.Name : "", buildDef.QueueStatus);

                ListBuilds(TeamProjectName, buildDef);
            }
        }

        /// <summary>
        /// Show builds details
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="buildDef"></param>
        private static void ListBuilds(string TeamProjectName, BuildDefinitionReference buildDef)
        {
            List<Build> builds = BuildClient.GetBuildsAsync(TeamProjectName, new List<int> { buildDef.Id }).Result;

            if (builds.Count > 0)
            {
                Console.WriteLine("+====================BUILDS================================================================================");
                Console.WriteLine("+    ID      |        NUMBER        |      STATUS     |     START DATE     |    FINISH DATE     | COMMITS");
                Console.WriteLine("+----------------------------------------------------------------------------------------------------------");

                for (int i = 0; i < builds.Count && i < 10; i++)
                {
                    var changes = BuildClient.GetBuildChangesAsync(TeamProjectName, builds[i].Id).Result;
                    Console.WriteLine(" {0, -12}|{1, -22}|{2, -17}|{3, -20}|{4, -20}|{5}", builds[i].Id, builds[i].BuildNumber, builds[i].Status,
                        (builds[i].StartTime.HasValue) ? builds[i].StartTime.Value.ToString() : "",
                        (builds[i].FinishTime.HasValue) ? builds[i].FinishTime.Value.ToString() : "", changes.Count);
                }
            }
            else
                Console.WriteLine("+=======================================================================================");
        }

        #region create new connections

        static void InitClients(VssConnection Connection)
        {
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            BuildClient = Connection.GetClient<BuildHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            GitClient = Connection.GetClient<GitHttpClient>();
            TfvsClient = Connection.GetClient<TfvcHttpClient>();
            TestManagementClient = Connection.GetClient<TestManagementHttpClient>();
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
