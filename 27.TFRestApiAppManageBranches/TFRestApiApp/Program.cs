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

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static TeamHttpClient TeamClient;
        static ReleaseHttpClient ReleaseClient;
        static IdentityHttpClient IdentityClient;

        static void Main(string[] args)
        {
            try
            {
                string TeamProjectName = "<team project name>";
                string GitRepoName = "<repo name>";
                string sourceBranch = "master";
                string targetBranch = "features/feature1";
                string textFilePath = "textfile.txt";

                ConnectWithPAT(TFUrl, UserPAT);

                CreateBranch(TeamProjectName, GitRepoName, sourceBranch, targetBranch);

                AddTextFile(TeamProjectName, GitRepoName, targetBranch, "", textFilePath, "New file");
                UpdateTextFile(TeamProjectName, GitRepoName, targetBranch, "", textFilePath, "New file\r\nNew line");
                LockBranch(TeamProjectName, GitRepoName, targetBranch);
                LockBranch(TeamProjectName, GitRepoName, targetBranch, false);
                GetDiff(TeamProjectName, GitRepoName, sourceBranch, targetBranch);
                CreateCompletePullRequest(TeamProjectName, GitRepoName, "refs/heads/" + targetBranch, "refs/heads/" + sourceBranch);
                RemoveBranch(TeamProjectName, GitRepoName, targetBranch);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Remove branch
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="targetBranch"></param>
        private static void RemoveBranch(string teamProjectName, string gitRepoName, string targetBranch)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/" + targetBranch).Result;

            if (gitBranches.Count != 1) return;

            GitRefUpdate refUpdate = new GitRefUpdate();
            refUpdate.OldObjectId = gitBranches[0].ObjectId;
            refUpdate.NewObjectId = "0000000000000000000000000000000000000000";
            refUpdate.Name = "refs/heads/" + targetBranch;

            var updateResult = GitClient.UpdateRefsAsync(new GitRefUpdate[] { refUpdate }, project: teamProjectName, repositoryId: gitRepoName).Result;

            foreach (var update in updateResult)
            {
                Console.WriteLine("Branch was removed {0}", update.Name);
            }
        }

        /// <summary>
        /// Get diffs between branches
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="sourceBranch"></param>
        /// <param name="targetBranch"></param>
        private static void GetDiff(string teamProjectName, string gitRepoName, string sourceBranch, string targetBranch)
        {
            var diffResult = GitClient.GetCommitDiffsAsync(teamProjectName, gitRepoName,
                baseVersionDescriptor: new GitBaseVersionDescriptor() { VersionType = GitVersionType.Branch, Version = sourceBranch },
                targetVersionDescriptor: new GitTargetVersionDescriptor() { VersionType = GitVersionType.Branch, Version = targetBranch }).Result;

            Console.WriteLine("Diffs between {0} and {1}", sourceBranch, targetBranch);
            Console.WriteLine("Ahead {0}; Behind {1}", diffResult.AheadCount, diffResult.BehindCount);

            Console.WriteLine("Changes {0}", diffResult.ChangeCounts.Count);
            foreach (var change in diffResult.ChangeCounts.Keys)
                Console.WriteLine("    {0}:{1}", change, diffResult.ChangeCounts[change]);

            Console.WriteLine("Changed items {0}", diffResult.Changes.Count());
            foreach (var changeditem in diffResult.Changes)
                Console.WriteLine("    {0} : {1} : {2}", changeditem.Item.Path, changeditem.Item.GitObjectType, changeditem.ChangeType);
        }

        /// <summary>
        /// Lock or unlock branch
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="targetBranch"></param>
        /// <param name="lockit"></param>
        private static void LockBranch(string teamProjectName, string gitRepoName, string targetBranch, bool lockit = true)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/" + targetBranch).Result;

            if (gitBranches.Count != 1) return;

            GitRefUpdate refUpdate = new GitRefUpdate();

            refUpdate.IsLocked = lockit;

            var lockResult = GitClient.UpdateRefAsync(refUpdate, project: teamProjectName, repositoryId: gitRepoName, filter: "heads/" + targetBranch).Result;

            Console.WriteLine("Branch {0} is {1} {2}", 
                lockResult.Name, 
                (lockResult.IsLocked) ? "locked" : "not locked", 
                (lockResult.IsLocked) ? "by " + lockResult.IsLockedBy.DisplayName : "");
        }

        /// <summary>
        /// Create new branch
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="sourceBranch"></param>
        /// <param name="targetBranch"></param>
        static void CreateBranch(string teamProjectName, string gitRepoName, string sourceBranch, string targetBranch)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/" + sourceBranch).Result;

            if (gitBranches.Count != 1) return;

            GitRefUpdate refUpdate = new GitRefUpdate();
            refUpdate.OldObjectId = "0000000000000000000000000000000000000000";
            refUpdate.NewObjectId = gitBranches[0].ObjectId;
            refUpdate.Name = "refs/heads/" + targetBranch;

            var updateResult = GitClient.UpdateRefsAsync(new GitRefUpdate[] { refUpdate },project: teamProjectName, repositoryId: gitRepoName).Result;

            foreach (var update in updateResult)
            {
                Console.WriteLine("Branch was created {0}", update.Name);
            }
        }


        /// <summary>
        /// Create and complete pull request
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="RepoName"></param>
        /// <param name="SourceRef"></param>
        /// <param name="TargetRef"></param>
        static void CreateCompletePullRequest(string TeamProjectName, string RepoName, string SourceRef, string TargetRef)
        {
            GitPullRequest pr = new GitPullRequest();
            pr.Title = pr.Description = String.Format("PR from {0} into {1} ", SourceRef, TargetRef);
            pr.SourceRefName = SourceRef;
            pr.TargetRefName = TargetRef;

            var newPr = GitClient.CreatePullRequestAsync(pr, TeamProjectName, RepoName).Result;

            Console.WriteLine("PR was created: " + newPr.PullRequestId);

            Thread.Sleep(5000);

            GitPullRequest prUdated = new GitPullRequest();
            prUdated.Status = PullRequestStatus.Completed;
            prUdated.LastMergeSourceCommit = newPr.LastMergeSourceCommit;

            prUdated = GitClient.UpdatePullRequestAsync(prUdated, TeamProjectName, RepoName, newPr.PullRequestId).Result;
        }

        /// <summary>
        /// Add new text file to branch
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="branchname"></param>
        /// <param name="textFilePath"></param>
        /// <param name="targetPath"></param>
        /// <param name="content"></param>
        private static void AddTextFile(string teamProjectName, string gitRepoName, string branchname, string textFilePath, string targetPath, string content = null)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/" + branchname).Result;

            if (gitBranches.Count == 1)
            {
                string fileContent = (content == null) ? File.ReadAllText(textFilePath) : content;

                PushChanges(
                    teamProjectName,
                    gitRepoName,
                    "refs/heads/" + branchname,
                    gitBranches[0].ObjectId,
                    targetPath,
                    fileContent,
                    ItemContentType.RawText,
                    VersionControlChangeType.Add,
                    "New text file");
            }
        }

        /// <summary>
        /// Update text file in branch
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="branchname"></param>
        /// <param name="textFilePath"></param>
        /// <param name="targetPath"></param>
        /// <param name="content"></param>
        private static void UpdateTextFile(string teamProjectName, string gitRepoName, string branchname, string textFilePath, string targetPath, string content = null)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/" + branchname).Result;

            if (gitBranches.Count == 1)
            {
                string fileContent = (content == null) ? File.ReadAllText(textFilePath) : content;

                PushChanges(
                    teamProjectName,
                    gitRepoName,
                    "refs/heads/" + branchname,
                    gitBranches[0].ObjectId,
                    targetPath,
                    fileContent,
                    ItemContentType.RawText,
                    VersionControlChangeType.Edit,
                    "Updated text file");
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
