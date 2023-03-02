using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.TeamFoundation.Wiki;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Wiki.WebApi;
using System.Management.Instrumentation;
using Microsoft.TeamFoundation.SourceControl.WebApi.Legacy;

namespace TFRestApiApp
{
    class Program
    {
        static readonly string TFUrl = "https://dev.azure.com/<your_or>";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<your_pat>"; ////https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=Windows

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static WikiHttpClient WikiClient;

        static void Main(string[] args)
        {
            try
            {
                string ProjectName = "<PrpojectName>";
                string WikiName = "<WikiName>";
                string NewWikiName = "<NewWikiName>";

                ConnectWithPAT(TFUrl, UserPAT);

                ViewCollectionWiki();

                CreateProjectWiki(ProjectName);

                CreateCodeWiki(ProjectName, WikiName);

                RenameWiki(ProjectName, WikiName, NewWikiName);

                RemoveWiki(ProjectName, NewWikiName);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// List all wikis in a org
        /// </summary>
        static void ViewCollectionWiki()
        {
            var wikiLIST = WikiClient.GetAllWikisAsync().Result;
            
            foreach (var wiki in wikiLIST)
            {
                Console.WriteLine(string.Format("{0} : {1}", GetProjectNameById(wiki.ProjectId.ToString()), wiki.Name));
            }
        }

        /// <summary>
        /// Rename wiki
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="OldWikiName"></param>
        /// <param name="NewWikiName"></param>
        static void RenameWiki(string ProjectName, string OldWikiName, string NewWikiName)
        {
            if (!CheckWiki(ProjectName, OldWikiName)) return;

            WikiUpdateParameters updateParameters = new WikiUpdateParameters();
            updateParameters.Name = NewWikiName;
            var wiki = WikiClient.UpdateWikiAsync(updateParameters, ProjectName, OldWikiName).Result;
            Console.WriteLine($@"New wiki name: {wiki.Name}");
        }

        /// <summary>
        /// Remove only wiki (without a repository).
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="WikiName"></param>
        static void RemoveWiki(string ProjectName, string WikiName)
        {
            if (!CheckWiki(ProjectName, WikiName)) return;

            WikiClient.DeleteWikiAsync(ProjectName, WikiName).Wait();
                        
            Console.WriteLine($@"Wiki removed: {WikiName}");
        }

        /// <summary>
        /// Create a project wiki if not exists.
        /// </summary>
        /// <param name="ProjectName"></param>
        static void CreateProjectWiki(string ProjectName)
        {
            if (CheckProjectWiki(ProjectName)) return;

            var project = ProjectClient.GetProject(ProjectName).Result;

            WikiCreateParametersV2 newWikiParams = new WikiCreateParametersV2();
            newWikiParams.Type = WikiType.ProjectWiki; 
            newWikiParams.Name = ProjectName + "11.wiki";
            newWikiParams.ProjectId = project.Id;

            var neWiki = WikiClient.CreateWikiAsync(newWikiParams).Result;

            Console.WriteLine($@"Wiki is created: ${neWiki.Name}");
        }

        /// <summary>
        /// Create a new code based wiki.
        /// Additionally, creates a git repo and master branch.
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="WikiName"></param>
        static void CreateCodeWiki(string ProjectName, string WikiName)
        {
            if (CheckWiki(ProjectName, WikiName)) return;

            var project = ProjectClient.GetProject(ProjectName).Result;

            string RepoName = WikiName + ".wiki";

            GitRepository gitRepo = CreateGitRepo(ProjectName, RepoName);
               

            WikiCreateParametersV2 newWikiParams = new WikiCreateParametersV2();
            newWikiParams.Type = WikiType.CodeWiki;
            newWikiParams.Name = WikiName;
            newWikiParams.RepositoryId = gitRepo.Id;
            newWikiParams.Version = new GitVersionDescriptor() { Version = "master", VersionType = GitVersionType.Branch };
            newWikiParams.MappedPath = "/";
            newWikiParams.ProjectId = project.Id;

            var neWiki = WikiClient.CreateWikiAsync(newWikiParams).Result;

            Console.WriteLine($@"Wiki is created: ${neWiki.Name}");
        }

        /// <summary>
        /// Check created project wiki
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <returns></returns>
        static bool CheckProjectWiki(string ProjectName)
        {
            var wikiLIST = WikiClient.GetAllWikisAsync(ProjectName).Result;

            var wiki = (from w in wikiLIST where w.Type == WikiType.ProjectWiki select w).FirstOrDefault();

            if (wiki == null) return false;

            return true;
        }

        /// <summary>
        /// Check created wiki in a project
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="WikiName"></param>
        /// <returns></returns>
        static bool CheckWiki(string ProjectName, string WikiName)
        {
            var wikiLIST = WikiClient.GetAllWikisAsync(ProjectName).Result;

            var wiki = (from w in wikiLIST where w.Name == WikiName select w).FirstOrDefault();

            if (wiki == null) return false;

            return true;
        }

        /// <summary>
        /// Create a repositiry and master branch for a wiki
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="RepoName"></param>
        /// <returns></returns>
        static GitRepository CreateGitRepo(string ProjectName, string RepoName)
        {
            GitRepository gitRepo = GetGitRepo(ProjectName, RepoName);

            if (gitRepo == null)
            {
                GitRepositoryCreateOptions gitRepoOptions = new GitRepositoryCreateOptions();

                gitRepoOptions.Name = RepoName;

                gitRepo = GitClient.CreateRepositoryAsync(gitRepoOptions, project: ProjectName).Result;

                Console.WriteLine($@"Repo is created: {gitRepo.Name}");
            }

            var gitBranch = GetGitRepoBranch(gitRepo.Id.ToString(), "master");

            if (gitBranch == null)
            {
                PushChanges(
                    ProjectName,
                    RepoName,
                    "refs/heads/master",
                    "0000000000000000000000000000000000000000",
                    "/readme.md",
                    "readme.md",
                    ItemContentType.RawText,
                    Microsoft.TeamFoundation.SourceControl.WebApi.VersionControlChangeType.Add,
                    "Initial commit.");

            }

            return gitRepo;
        }


        /// <summary>
        /// Get project name by its Id
        /// </summary>
        /// <param name="ProjectId"></param>
        /// <returns></returns>
        static string GetProjectNameById(string ProjectId)
        {
            return ProjectClient.GetProject(ProjectId).Result.Name;
        }

        /// <summary>
        /// Find a git repository
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="RepoName"></param>
        /// <returns></returns>
        static GitRepository GetGitRepo(string ProjectName, string RepoName)
        {
            var repoLIST = GitClient.GetRepositoriesAsync(ProjectName).Result;

            return (from w in repoLIST where w.Name == RepoName select w).FirstOrDefault();            
        }

        /// <summary>
        /// Find a branch
        /// </summary>
        /// <param name="RepoId"></param>
        /// <param name="BranchName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        static GitBranchStats GetGitRepoBranch(string RepoId, string BranchName)
        {
            try
            {
                var branchesLIST = GitClient.GetBranchesAsync(RepoId).Result;

                return (from w in branchesLIST where w.Name == BranchName select w).FirstOrDefault();
            }
            catch(Exception ex)
            {
                if (ex.InnerException != null && ex.InnerException.Message.Contains("VS403403: Cannot find any branches"))
                    return null;
                else throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Push changes to branch
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
        private static void PushChanges(string teamProjectName, string gitRepoName, string branchRef, string branchOldId, string targetPath, string fileContent, ItemContentType itemContentType, Microsoft.TeamFoundation.SourceControl.WebApi.VersionControlChangeType versionControlChangeType, string commitComment)
        {
            GitRefUpdate branch = new GitRefUpdate();
            branch.OldObjectId = branchOldId;
            branch.Name = branchRef;

            GitCommitRef newCommit = new GitCommitRef();
            newCommit.Comment = commitComment;
            var gitChange = new Microsoft.TeamFoundation.SourceControl.WebApi.GitChange();
            gitChange.ChangeType = versionControlChangeType;
            gitChange.Item = new Microsoft.TeamFoundation.SourceControl.WebApi.GitItem() { Path = targetPath };
            gitChange.NewContent = new ItemContent() { Content = fileContent, ContentType = itemContentType };
            newCommit.Changes = new Microsoft.TeamFoundation.SourceControl.WebApi.GitChange[] { gitChange };

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
            WikiClient = Connection.GetClient<WikiHttpClient>();
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
