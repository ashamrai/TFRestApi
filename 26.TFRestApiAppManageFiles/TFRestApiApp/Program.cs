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
                string TeamProjectName = "<team_project_name>";
                string GitRepoName = "<repo_name>";
                string TextFilePath = "<local_text_file_path>";
                string TextFileTargetPath = "/textfiles/textfile.txt";
                string TextFileNewTargetPath = "/textfilesnew/textfile.txt";
                string BinaryFilePath = "<local_zip_file_path>";
                string BinaryFileTargetPath = "/binaryfiles/archive.zip";


                ConnectWithPAT(TFUrl, UserPAT);

                InitialCommit(TeamProjectName, GitRepoName);
                AddTextFile(TeamProjectName, GitRepoName, TextFilePath, TextFileTargetPath);
                UpdateTextFile(TeamProjectName, GitRepoName, TextFilePath, TextFileTargetPath);
                AddBinaryFile(TeamProjectName, GitRepoName, BinaryFilePath, BinaryFileTargetPath);
                UpdateBinaryFile(TeamProjectName, GitRepoName, BinaryFilePath, BinaryFileTargetPath);
                DeleteFile(TeamProjectName, GitRepoName, BinaryFileTargetPath);
                RenameOrMoveFile(TeamProjectName, GitRepoName, TextFileTargetPath, TextFileNewTargetPath);                
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Rename or move file in a repo
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="fileOldPath"></param>
        /// <param name="fileTargetPath"></param>
        private static void RenameOrMoveFile(string teamProjectName, string gitRepoName, string fileOldPath, string fileTargetPath)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/main").Result;

            if (gitBranches.Count == 1)
            {

                PushChanges(
                    teamProjectName,
                    gitRepoName,
                    "refs/heads/main",
                    gitBranches[0].ObjectId,
                    fileOldPath,
                    fileTargetPath,
                    VersionControlChangeType.Rename,
                    "Move file");
            }
        }

        /// <summary>
        /// Remove file from a repo
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="fileTargetPath"></param>
        private static void DeleteFile(string teamProjectName, string gitRepoName, string fileTargetPath)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/main").Result;

            if (gitBranches.Count == 1)
            {

                PushChanges(
                    teamProjectName,
                    gitRepoName,
                    "refs/heads/main",
                    gitBranches[0].ObjectId,
                    null,
                    fileTargetPath,                    
                    VersionControlChangeType.Delete,
                    "Remove file");
            }
        }

        /// <summary>
        /// Add text content
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="textFilePath"></param>
        /// <param name="targetPath"></param>
        private static void AddTextFile(string teamProjectName, string gitRepoName, string textFilePath, string targetPath)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/main").Result;

            if (gitBranches.Count == 1)
            {
                string fileContent = File.ReadAllText(textFilePath);

                PushChanges(
                    teamProjectName,
                    gitRepoName,
                    "refs/heads/main",
                    gitBranches[0].ObjectId,
                    targetPath,
                    fileContent,
                    ItemContentType.RawText,
                    VersionControlChangeType.Add,
                    "New text file");
            }
        }

        /// <summary>
        /// Add binary content
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="binaryFilePath"></param>
        /// <param name="targetPath"></param>
        private static void AddBinaryFile(string teamProjectName, string gitRepoName, string binaryFilePath, string targetPath)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/main").Result;

            if (gitBranches.Count == 1)
            {
                byte[] fileContent = File.ReadAllBytes(binaryFilePath);
                string fileBase64Content = Convert.ToBase64String(fileContent);

                PushChanges(
                    teamProjectName,
                    gitRepoName,
                    "refs/heads/main",
                    gitBranches[0].ObjectId,
                    targetPath,
                    fileBase64Content,
                    ItemContentType.Base64Encoded,
                    VersionControlChangeType.Add,
                    "New binary file");
            }
        }

        /// <summary>
        /// Update binary content
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="binaryFilePath"></param>
        /// <param name="targetPath"></param>
        private static void UpdateBinaryFile(string teamProjectName, string gitRepoName, string binaryFilePath, string targetPath)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/main").Result;

            if (gitBranches.Count == 1)
            {
                byte[] fileContent = File.ReadAllBytes(binaryFilePath);
                string fileBase64Content = Convert.ToBase64String(fileContent);

                PushChanges(
                    teamProjectName,
                    gitRepoName,
                    "refs/heads/main",
                    gitBranches[0].ObjectId,
                    targetPath,
                    fileBase64Content,
                    ItemContentType.Base64Encoded,
                    VersionControlChangeType.Edit,
                    "Updated binary file");
            }
        }

        /// <summary>
        /// Update text content
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="textFilePath"></param>
        /// <param name="targetPath"></param>
        private static void UpdateTextFile(string teamProjectName, string gitRepoName, string textFilePath, string targetPath)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/main").Result;

            if (gitBranches.Count == 1)
            {
                string fileContent = File.ReadAllText(textFilePath);

                PushChanges(
                    teamProjectName,
                    gitRepoName,
                    "refs/heads/main",
                    gitBranches[0].ObjectId,
                    targetPath,
                    fileContent,
                    ItemContentType.RawText,
                    VersionControlChangeType.Edit,
                    "Updated text file");
            }
        }

        /// <summary>
        /// Add file to an empty repo
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        private static void InitialCommit(string teamProjectName, string gitRepoName)
        {
            PushChanges(
                teamProjectName,
                gitRepoName,
                "refs/heads/main",
                "0000000000000000000000000000000000000000",
                "/readme.md",
                "My first file!",
                ItemContentType.RawText,
                VersionControlChangeType.Add,
                "Initial commit");

        }

        /// <summary>
        /// Push changes to remove, rename or move files
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="branchRef"></param>
        /// <param name="branchOldId"></param>
        /// <param name="oldPath"></param>
        /// <param name="targetPath"></param>
        /// <param name="versionControlChangeType"></param>
        /// <param name="commitComment"></param>
        private static void PushChanges(string teamProjectName, string gitRepoName, string branchRef, string branchOldId, string oldPath, string targetPath, VersionControlChangeType versionControlChangeType, string commitComment)
        {
            GitRefUpdate branch = new GitRefUpdate();
            branch.OldObjectId = branchOldId;
            branch.Name = branchRef;

            GitCommitRef newCommit = new GitCommitRef();
            newCommit.Comment = commitComment;
            GitChange gitChange = new GitChange();
            gitChange.ChangeType = versionControlChangeType;
            if (!string.IsNullOrEmpty(oldPath)) gitChange.SourceServerItem = oldPath;
            gitChange.Item = new GitItem() { Path = targetPath };
            newCommit.Changes = new GitChange[] { gitChange };

            GitPush gitPush = new GitPush();
            gitPush.RefUpdates = new GitRefUpdate[] { branch };
            gitPush.Commits = new GitCommitRef[] { newCommit };

            var pushResult = GitClient.CreatePushAsync(gitPush, teamProjectName, gitRepoName).Result;

            Console.WriteLine("Push id: " + pushResult.PushId);
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
