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

                CloneStandardBuild(TeamProjectName);
                CreateYamlBuild(TeamProjectName);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Clone existing build definition
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void CloneStandardBuild(string TeamProjectName)
        {
            int SourceBuildId = 22; // id of existing build definition
            string NewName = "ClonedBuildDef"; 
            string NewPath = "Cloned"; 
            string GitRepoName = "Second";
            string GitRepoFormat = @"https://<org>@dev.azure.com/<org>/{0}/_git/{1}"; // link to clone a git repo
            string NewBranch = null; // example for branch dev: refs/heads/dev
            string NewProjectPath = null; // example: "New Folder/New project.sln";

            var bld = BuildClient.GetDefinitionAsync(TeamProjectName, SourceBuildId).Result;

            var clonedBuild = bld;

            clonedBuild.Repository.Url = new Uri(String.Format(GitRepoFormat, TeamProjectName, GitRepoName));
            clonedBuild.Repository.Name = GitRepoName;
            clonedBuild.Repository.Id = null;
            if (NewBranch != null) clonedBuild.Repository.DefaultBranch = NewBranch;
            clonedBuild.Path = NewPath;
            clonedBuild.Name = NewName;

            if (NewProjectPath != null && clonedBuild.ProcessParameters.Inputs.Count == 1)
                clonedBuild.ProcessParameters.Inputs[0].DefaultValue = NewProjectPath;

            clonedBuild = BuildClient.CreateDefinitionAsync(clonedBuild, TeamProjectName).Result;

            Console.WriteLine("The build definition has been created");
            Console.WriteLine("Build Id: {0}\nBuild Name: {1}\n Build Path: {2}", clonedBuild.Id, clonedBuild.Name, clonedBuild.Path);
        }

        /// <summary>
        /// Create build definition based on yaml file
        /// </summary>
        /// <param name="TeamProjectName"></param>
        static void CreateYamlBuild(string TeamProjectName)
        {
            string BuildName = "NewBuildDef";
            string BuildPath = "Cloned";
            string GitRepoName = "YamlBuildDefRepo";
            string GitRepoFormat = @"https://<org>@dev.azure.com/<org>/{0}/_git/{1}"; // link to clone a git repo
            string RepoBranch = "refs/heads/master"; // example for branch dev: refs/heads/dev
            string SlnPath = "New Folder/New project.sln";

            var bld = BuildClient.GetDefinitionAsync(TeamProjectName, 28).Result;            

            BuildDefinition newBuild = new BuildDefinition();
            newBuild.Path = BuildPath;
            newBuild.Name = BuildName;
            newBuild.Queue = new AgentPoolQueue() { Name = "Hosted VS2017" };

            YamlProcess yamlProcess = new YamlProcess();
            yamlProcess.YamlFilename = "<yaml file>";
            newBuild.Process = yamlProcess;

            newBuild.Repository = new BuildRepository();
            newBuild.Repository.Url = new Uri(String.Format(GitRepoFormat, TeamProjectName, GitRepoName));
            newBuild.Repository.Name = GitRepoName;
            newBuild.Repository.DefaultBranch = RepoBranch;
            newBuild.Repository.Type = RepositoryTypes.TfsGit;

            newBuild.Variables.Add("BuildConfiguration", new BuildDefinitionVariable { AllowOverride = false, IsSecret = false, Value = "Debug" });
            newBuild.Variables.Add("BuildPlatform", new BuildDefinitionVariable { AllowOverride = false, IsSecret = false, Value = "Any CPU" });
            newBuild.Variables.Add("Parameters.solution", new BuildDefinitionVariable { AllowOverride = false, IsSecret = false, Value = SlnPath });

            newBuild = BuildClient.CreateDefinitionAsync(newBuild, TeamProjectName).Result;

            Console.WriteLine("The build definition has been created");
            Console.WriteLine("Build Id: {0}\nBuild Name: {1}\n Build Path: {2}", newBuild.Id, newBuild.Name, newBuild.Path);
        }

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
    }
}
