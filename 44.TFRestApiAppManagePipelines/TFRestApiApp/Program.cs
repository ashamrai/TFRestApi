using Microsoft.Azure.Pipelines.WebApi;
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
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    class Program
    {
        static readonly string TFUrl = "https://dev.azure.com/<org>";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=Windows

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static PipelinesHttpClient PipelinesClient;

        static void Main(string[] args)
        {
            string teamProjectName = "<Team Project Name>";
            string repoName = "<Repo Name>";
            string exisitingYamlPath = "<yaml repo releative path>"; // starts with '/'
            string pipelineName = "<New Pipeline Name>";

            ConnectWithPAT(TFUrl, UserPAT);

            var newId = CreateExistingYaml(teamProjectName, repoName, exisitingYamlPath, pipelineName);

            ListPipelines(teamProjectName);

            DeletePipeline(teamProjectName, newId);

        }

        /// <summary>
        /// Delete pipline through BuildHttpClient
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="newId"></param>
        private static void DeletePipeline(string teamProjectName, int newId)
        {
            BuildClient.DeleteDefinitionAsync(teamProjectName, newId).Wait();
        }

        /// <summary>
        /// Create a new pipeline based on an existing yaml file
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="repoName"></param>
        /// <param name="exisitingYamlPath"></param>
        /// <param name="piplinename"></param>
        /// <returns></returns>
        private static int CreateExistingYaml(string teamProjectName, string repoName, string exisitingYamlPath, string piplinename)
        {
            var repo = GitClient.GetRepositoryAsync(teamProjectName, repoName).Result;
            CreateYamlPipelineConfigurationParameters cpparams = new CreateYamlPipelineConfigurationParameters();
            cpparams.Path = exisitingYamlPath;
            cpparams.Repository = new CreateAzureReposGitRepositoryParameters() { Name = repo.Name, Id = repo.Id };

            CreatePipelineParameters pparams = new CreatePipelineParameters { Configuration = cpparams, Folder = "/", Name = piplinename };

            var pipeline = PipelinesClient.CreatePipelineAsync(pparams, teamProjectName).Result;

            Console.WriteLine($@"Pipeline is created {pipeline.Id}");

            return pipeline.Id;
        }

        /// <summary>
        /// List all pipelines in a project
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void ListPipelines(string TeamProjectName)
        {
            var pipelines = PipelinesClient.ListPipelinesAsync(TeamProjectName).Result;

            foreach (var pipeline in pipelines)
            {
                var pipelineDetailes = PipelinesClient.GetPipelineAsync(TeamProjectName, pipeline.Id).Result;
                Console.WriteLine($@"{pipeline.Id} - {pipeline.Name} - {pipelineDetailes.Configuration.Type} - {pipeline.Folder}");                    
            }
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
            PipelinesClient = Connection.GetClient<PipelinesHttpClient>();
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
