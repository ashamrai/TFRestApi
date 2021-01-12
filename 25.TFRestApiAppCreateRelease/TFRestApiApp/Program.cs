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
        static TeamHttpClient TeamClient;
        static ReleaseHttpClient ReleaseClient;

        static void Main(string[] args)
        {
            try
            {
                string TeamProjectName = "<TeamProjectName>";
                int releaseDefId = -1; //update to use your realease definition

                ConnectWithPAT(TFUrl, UserPAT);

                int releaseId = CreateRelease(TeamProjectName, releaseDefId);

                for (int i = 0; i < 10; i++)
                {
                    Thread.Sleep(10000);

                    CheckStatus(TeamProjectName, releaseId);
                }

                DownloadReleaseLogs(TeamProjectName, releaseId);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Check status of created release
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="releaseId"></param>
        private static void CheckStatus(string teamProjectName, int releaseId)
        {
            var release = ReleaseClient.GetReleaseAsync(teamProjectName, releaseId).Result;
            
            Console.WriteLine("\nStatus: " + release.Status + "\nEnvironments:");

            foreach(var env in release.Environments)
            {
                Console.Write(env.Name + " : " + env.Status + "; ");
            }         
        }

        /// <summary>
        /// Download zip archive with logs
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="releaseId"></param>
        private static void DownloadReleaseLogs(string teamProjectName, int releaseId)
        {
            Stream logReader = ReleaseClient.GetLogsAsync(teamProjectName, releaseId).Result;

            using (var fileStream = new FileStream("C:\\Temp\\rel_" + releaseId + "_logs.zip", FileMode.Create))
            {
                logReader.CopyTo(fileStream);
            }
        }

        /// <summary>
        /// Create release for release definition
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="releaseDefId"></param>
        /// <returns></returns>
        private static int CreateRelease(string teamProjectName, int releaseDefId)
        {
            ReleaseStartMetadata startMetadata = new ReleaseStartMetadata();
            startMetadata.DefinitionId = releaseDefId;
            startMetadata.Description = "Start from command line";

            var release = ReleaseClient.CreateReleaseAsync(startMetadata, teamProjectName).Result;

            return release.Id;
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
