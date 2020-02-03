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
        static TeamHttpClient TeamClient;

        static void Main(string[] args)
        {
            try
            {
                string TeamProjectName = "<Team Project Name>";
                string GitNewRepoName = "<Repo Name 1>";
                string GitNewForkRepoName = "<Repo Name 2>";

                ConnectWithPAT(TFUrl, UserPAT);

                CreateGitRepo(TeamProjectName, GitNewRepoName);
                CreateGitRepo(TeamProjectName, GitNewForkRepoName, GitNewRepoName);

                PrintAllRepos(TeamProjectName);

                RemoveRepoToRecycleBin(TeamProjectName, GitNewForkRepoName);
                RemoveRepoToRecycleBin(TeamProjectName, GitNewRepoName);

                RestoreRepo(TeamProjectName, GitNewRepoName);

                ClearRecycleBin(TeamProjectName);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Restore git repo from the Recycle Bin
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="GitRepoName"></param>
        private static void RestoreRepo(string TeamProjectName, string GitRepoName)
        {
            List<GitDeletedRepository> repos = GitClient.GetRecycleBinRepositoriesAsync(TeamProjectName).Result;

            if (repos.Count == 0) return;

            var repotorestore = repos.FirstOrDefault(x => x.Name == GitRepoName);

            if (repotorestore != null)
            {
                GitClient.RestoreRepositoryFromRecycleBinAsync(new GitRecycleBinRepositoryDetails { Deleted = false },
                    TeamProjectName, repotorestore.Id).Wait();

                Console.WriteLine("Restored repo: " + GitRepoName);
            }
        }

        /// <summary>
        /// Clear the Recycle Bin
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void ClearRecycleBin(string TeamProjectName)
        {
            List<GitDeletedRepository> repos = GitClient.GetRecycleBinRepositoriesAsync(TeamProjectName).Result;

            foreach (var repo in repos)
                GitClient.DeleteRepositoryFromRecycleBinAsync(TeamProjectName, repo.Id).Wait();

            Console.WriteLine("The recycle bin is empty");
        }

        /// <summary>
        /// Delete a repo
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="RepoName"></param>
        private static void RemoveRepoToRecycleBin(string TeamProjectName, string RepoName)
        {
            GitRepository gitRepo = GitClient.GetRepositoryAsync(TeamProjectName, RepoName).Result;
            GitClient.DeleteRepositoryAsync(gitRepo.Id).Wait();

            Console.WriteLine("Removed repo: " + RepoName);
        }

        /// <summary>
        /// View all git repos in the team projetc
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void PrintAllRepos(string TeamProjectName)
        {
            List<GitRepository> repos = GitClient.GetRepositoriesAsync(TeamProjectName, includeLinks: true).Result;

            foreach (var repo in repos)
            {
                Console.WriteLine("==================Git Repo List===============================");
                PrintRepoInfo(repo);
                Console.WriteLine("==============================================================");
            }
        }

        /// <summary>
        /// Create new git repo
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="GitNewRepoName"></param>
        /// <param name="ParentRepo"></param>
        private static void CreateGitRepo(string TeamProjectName, string GitNewRepoName, string ParentRepo = null)
        {
            GitRepository newRepo;

            if (ParentRepo != null)
            {
                GitRepositoryCreateOptions newGitRepository = new GitRepositoryCreateOptions();
                newGitRepository.Name = GitNewRepoName;
                GitRepository parent = GitClient.GetRepositoryAsync(TeamProjectName, ParentRepo).Result;
                newGitRepository.ParentRepository = new GitRepositoryRef();
                newGitRepository.ParentRepository.Id = parent.Id;
                newGitRepository.ParentRepository.ProjectReference = parent.ProjectReference;
                newRepo = GitClient.CreateRepositoryAsync(newGitRepository, TeamProjectName, "refs/heads/master").Result;
            }
            else
            {
                newRepo = new GitRepository();
                newRepo.Name = GitNewRepoName;
                newRepo = GitClient.CreateRepositoryAsync(newRepo, TeamProjectName).Result;
            }


            Console.WriteLine("===============Created New Repo===============================");
            PrintRepoInfo(newRepo);
            Console.WriteLine("==============================================================");
        }

        /// <summary>
        /// Print the repo properties
        /// </summary>
        /// <param name="GitRepo"></param>
        private static void PrintRepoInfo(GitRepository GitRepo)
        {
            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine("                 GIT REPO: " + GitRepo.Name);
            Console.WriteLine("--------------------------------------------------------------");
            if (GitRepo.IsFork)
            {
                GitRepo = GitClient.GetRepositoryWithParentAsync(GitRepo.Id, true).Result;
                Console.WriteLine("Parent name      : " + GitRepo.ParentRepository.Name);
                Console.WriteLine("Parent remote url: " + GitRepo.ParentRepository.RemoteUrl);
            }
            Console.WriteLine("Remote url   : " + GitRepo.RemoteUrl);
            Console.WriteLine("Size         : " + GitRepo.Size);
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
