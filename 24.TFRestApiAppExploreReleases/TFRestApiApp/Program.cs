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
                string TeamProjectName = "<team project>";

                ConnectWithPAT(TFUrl, UserPAT);

                ViewProjectReleasDefinitions(TeamProjectName);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// View all releases in a tream project
        /// </summary>
        /// <param name="teamProjectName"></param>
        private static void ViewProjectReleasDefinitions(string teamProjectName)
        {
            var reldefList = ReleaseClient.GetReleaseDefinitionsAsync(teamProjectName, expand: ReleaseDefinitionExpands.Environments).Result;            

            foreach (var reldef in reldefList)
            {
                Console.WriteLine("===========RELEASE DEFINITION==================================");
                Console.WriteLine("ID: {0} PATH: {1} NAME: {2}", reldef.Id, reldef.Path, reldef.Name);
                Console.WriteLine("---------------------------------------------------------------");
                Console.Write("STAGES:");

                if (reldef.Environments != null) foreach (var env in reldef.Environments) Console.Write(env.Name + "; ");

                Console.WriteLine();
                ViewReleases(teamProjectName, reldef.Id);
            }
        }

        /// <summary>
        /// View top 10 releases
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="reldefId"></param>
        private static void ViewReleases(string teamProjectName, int reldefId)
        {
            var rels = ReleaseClient.GetReleasesAsync(teamProjectName, reldefId, expand: ReleaseExpands.Environments, top: 10).Result;

            foreach (var rel in rels)
            {
                Console.WriteLine("-----------RELEASE---------------------------------------------");
                Console.WriteLine("ID: {0} REASON: {1} NAME: {2}", rel.Id, rel.Reason, rel.Name);
                Console.WriteLine("---------------------------------------------------------------");
                Console.WriteLine("STAGES:");

                if (rel.Environments != null) foreach (var env in rel.Environments) Console.Write(env.Name + " : " + env.Status + "; ");

                Console.WriteLine();
                ViewBuildArtifacts(teamProjectName, rel.Id);
            }
        }

        /// <summary>
        /// View build artifacts
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="relId"></param>
        private static void ViewBuildArtifacts(string teamProjectName, int relId)
        {
            var relDetails = ReleaseClient.GetReleaseAsync(teamProjectName, relId).Result;

            if (relDetails.Artifacts != null)
            {
                foreach (var artifact in relDetails.Artifacts)
                {
                    if (artifact.Type == "Build")
                    {
                        Console.WriteLine("Build artifact: REPO - {0}; BRANCH - {1}", 
                            artifact.DefinitionReference["repository"].Name, 
                            artifact.DefinitionReference["branch"].Name);

                        int buildId;
                        if (Int32.TryParse(artifact.DefinitionReference["version"].Name, out buildId))
                        {
                            var commits = BuildClient.GetBuildChangesAsync(teamProjectName, buildId, top: 10).Result;
                            var workItems = BuildClient.GetBuildWorkItemsRefsAsync(teamProjectName, buildId, top: 10).Result;

                            Console.WriteLine("BUILDID   : {0}", buildId);
                            Console.WriteLine("COMMITS   : {0}", String.Join("; ", from x in commits select x.Id.Substring(0, 8)));
                            Console.WriteLine("WORK ITEMS: {0}", String.Join("; ", from x in workItems select x.Id));
                        }
                    }
                }
            }
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
