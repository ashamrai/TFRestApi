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
                string TeamProjectName = "<team_project_name>";
                string GitRepoName = "<repo_name>";
                string sourceBranch = "master";
                string tagName1 = "v_1.0", tagName2 = "v_1.1";
                string tagComment = "tag is created via reats api";
                string textFilePath = "txtfile.txt";

                ConnectWithPAT(TFUrl, UserPAT);

                CreateTag(TeamProjectName, GitRepoName, sourceBranch, tagName1, tagComment);

                AddTextFile(TeamProjectName, GitRepoName, sourceBranch, "", textFilePath, "New file");
                UpdateTextFile(TeamProjectName, GitRepoName, sourceBranch, "", textFilePath, "New file\r\nNew line");

                CreateTag(TeamProjectName, GitRepoName, sourceBranch, tagName2, tagComment);

                GetDiff(TeamProjectName, GitRepoName, tagName1, tagName2);
                RemoveTag(TeamProjectName, GitRepoName, tagName1);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }


        /// <summary>
        /// Remove tag
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="tagName"></param>
        private static void RemoveTag(string teamProjectName, string gitRepoName, string tagName)
        {
            var tags = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "tags/" + tagName).Result;

            if (tags.Count != 1) return;

            GitRefUpdate refUpdate = new GitRefUpdate();
            refUpdate.OldObjectId = tags[0].ObjectId;
            refUpdate.NewObjectId = "0000000000000000000000000000000000000000";
            refUpdate.Name = "refs/tags/" + tagName;

            var updateResult = GitClient.UpdateRefsAsync(new GitRefUpdate[] { refUpdate }, project: teamProjectName, repositoryId: gitRepoName).Result;

            foreach (var update in updateResult)
            {
                Console.WriteLine("Tag was removed {0}", update.Name);
            }
        }

        /// <summary>
        /// Get diff between two tags
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="baseTag"></param>
        /// <param name="targetTag"></param>
        private static void GetDiff(string teamProjectName, string gitRepoName, string baseTag, string targetTag)
        {
            var diffResult = GitClient.GetCommitDiffsAsync(teamProjectName, gitRepoName,
                baseVersionDescriptor: new GitBaseVersionDescriptor() { VersionType = GitVersionType.Tag, Version = baseTag },
                targetVersionDescriptor: new GitTargetVersionDescriptor() { VersionType = GitVersionType.Tag, Version = targetTag }).Result;

            Console.WriteLine("Diffs between {0} and {1}", baseTag, targetTag);
            Console.WriteLine("Ahead {0}; Behind {1}", diffResult.AheadCount, diffResult.BehindCount);

            Console.WriteLine("Changes {0}", diffResult.ChangeCounts.Count);
            foreach (var change in diffResult.ChangeCounts.Keys)
                Console.WriteLine("    {0}:{1}", change, diffResult.ChangeCounts[change]);

            Console.WriteLine("Changed items {0}", diffResult.Changes.Count());
            foreach (var changeditem in diffResult.Changes)
                Console.WriteLine("    {0} : {1} : {2}", changeditem.Item.Path, changeditem.Item.GitObjectType, changeditem.ChangeType);
        }

        /// <summary>
        /// Create new tag
        /// </summary>
        /// <param name="teamProjectName"></param>
        /// <param name="gitRepoName"></param>
        /// <param name="sourceBranch"></param>
        /// <param name="tagName"></param>
        /// <param name="tagMessage"></param>
        static void CreateTag(string teamProjectName, string gitRepoName, string sourceBranch, string tagName, string tagMessage)
        {
            var gitBranches = GitClient.GetRefsAsync(teamProjectName, gitRepoName, "heads/" + sourceBranch).Result;

            if (gitBranches.Count != 1) return;

            GitAnnotatedTag annotatedTag = new GitAnnotatedTag();
            annotatedTag.Name = tagName;
            annotatedTag.Message = tagMessage;
            annotatedTag.TaggedObject = new GitObject() { ObjectId = gitBranches[0].ObjectId };

            var newTag = GitClient.CreateAnnotatedTagAsync(annotatedTag, teamProjectName, gitRepoName).Result;

            Console.WriteLine("Tag was created {0}", newTag.Name);
            
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
