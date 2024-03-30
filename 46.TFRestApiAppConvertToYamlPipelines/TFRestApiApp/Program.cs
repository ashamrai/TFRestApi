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
            int sourceBuildId = -1;
            string repoName = "Repo Name";
            string newYamlPath = "<new yaml file>.yml"; // starts with '/'
            string pipelineName = "<New Pipeline Name>";

            ConnectWithPAT(TFUrl, UserPAT);

            //Get Source definition
            var buildDef = BuildClient.GetDefinitionAsync(teamProjectName, sourceBuildId).Result;

            //get yaml description
            var yamldef = BuildClient.GetDefinitionYamlAsync(teamProjectName, sourceBuildId).Result;

            //add new yaml file to SC
            AddTextFile(teamProjectName, repoName, yamldef.Yaml, newYamlPath);

            //create new pipeline based on the new yaml file
            var newId = CreateExistingYaml(teamProjectName, repoName, newYamlPath, pipelineName);

            //replicate variables frjm the source pipeline to the new one
            var newBuildDef = BuildClient.GetDefinitionAsync(teamProjectName, newId).Result;

            foreach (var buildVar in buildDef.Variables) 
                newBuildDef.Variables.Add(buildVar.Key, buildVar.Value);

            //disable new pipeline to verify its settings first
            newBuildDef.QueueStatus = DefinitionQueueStatus.Disabled;

            BuildClient.UpdateDefinitionAsync(newBuildDef).Wait();
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
        /// Add text content to master branch
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="textFilePath"></param>
        /// <param name="targetPath"></param>
        private static void AddTextFile(string teamProjectName, string gitRepoName, string fileContent, string targetPath)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/master").Result;

            if (gitBranches.Count == 1)
            {
                PushChanges(
                    teamProjectName,
                    gitRepoName,
                    "refs/heads/master",
                    gitBranches[0].ObjectId,
                    targetPath,
                    fileContent,
                    ItemContentType.RawText,
                    VersionControlChangeType.Add,
                    "New yaml file");
            }
        }

        /// <summary>
        /// push changes to add or update content
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="branchRef"></param>
        /// <param name="branchOldId"></param>
        /// <param name="targetPath"></param>
        /// <param name="fileContent"></param>
        /// <param name="itemContentType"></param>
        /// <param name="versionControlChangeType"></param>
        /// <param name="commitComment"></param>
        private static void PushChanges(string teamProjectName, string gitRepoName, string branchRef, string branchOldId, string targetPath, string fileContent, ItemContentType itemContentType, VersionControlChangeType versionControlChangeType, string commitComment)
        {
            GitRefUpdate branch = new GitRefUpdate();
            branch.OldObjectId = branchOldId;
            branch.Name = branchRef;

            GitCommitRef newCommit = new GitCommitRef();
            newCommit.Comment = commitComment;
            GitChange gitChange = new GitChange();
            gitChange.ChangeType = versionControlChangeType;
            gitChange.Item = new GitItem() { Path = targetPath };
            gitChange.NewContent = new ItemContent() { Content = fileContent, ContentType = itemContentType };
            newCommit.Changes = new GitChange[] { gitChange };

            GitPush gitPush = new GitPush();
            gitPush.RefUpdates = new GitRefUpdate[] { branch };
            gitPush.Commits = new GitCommitRef[] { newCommit };

            var pushResult = GitClient.CreatePushAsync(gitPush, teamProjectName, gitRepoName).Result;

            Console.WriteLine("Push id: " + pushResult.PushId);
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
