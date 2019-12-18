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
                string TeamProjectName = "<TEam_Project_Name>";
                string ReviewerDisplayName = "<User_display_name>";
                string GitRepo = "<GIT_REPO_NAME>";
                string SourceRef = "refs/heads/<source_branch>", TargetRef = "refs/heads/<target_branch>";
                int[] wiIds = { };

                ConnectWithPAT(TFUrl, UserPAT);

                string ReviewerId = GetUserId(TeamProjectName, ReviewerDisplayName);

                int prId = CreatePullRequest(TeamProjectName, GitRepo, SourceRef, TargetRef, wiIds, ReviewerId);
                AbandonPR(TeamProjectName, GitRepo, prId);
                prId = CreatePullRequest(TeamProjectName, GitRepo, SourceRef, TargetRef, wiIds, ReviewerId);
                CompletePR(TeamProjectName, GitRepo, prId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Get User from the Default Team
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="USerDisplayName"></param>
        /// <returns></returns>
        static string GetUserId(string TeamProjectName, string UserDisplayName)
        {
            List<TeamMember> teamMembers = TeamClient.GetTeamMembersWithExtendedPropertiesAsync(TeamProjectName, TeamProjectName + " Team").Result;

            var users = from x in teamMembers where x.Identity.DisplayName == UserDisplayName select x.Identity.Id;

            if (users.Count() == 1)
                return users.First();

            return "";
        }


        /// <summary>
        ///  Create new Pull Request
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="RepoName"></param>
        /// <param name="SourceRef"></param>
        /// <param name="TargetRef"></param>
        /// <param name="WorkItems"></param>
        /// <returns></returns>
        static int CreatePullRequest(string TeamProjectName, string RepoName, string SourceRef, string TargetRef, int [] WorkItems, string ReviewerId)
        {
            GitPullRequest pr = new GitPullRequest();
            pr.Title = pr.Description = String.Format("PR from {0} into {1} ", SourceRef, TargetRef);
            pr.SourceRefName = SourceRef;
            pr.TargetRefName = TargetRef;

            if (ReviewerId != "")
            {
                IdentityRefWithVote[] identityRefWithVotes = { new IdentityRefWithVote { Id = ReviewerId } };
                pr.Reviewers = identityRefWithVotes;
            }

            if (WorkItems != null && WorkItems.Length > 0)
            {
                List<ResourceRef> wiRefs = new List<ResourceRef>();

                foreach (int wiId in WorkItems)
                {
                    WorkItem workItem = WitClient.GetWorkItemAsync(wiId).Result;

                    wiRefs.Add(new ResourceRef { Id = workItem.Id.ToString(), Url = workItem.Url });
                }

                pr.WorkItemRefs = wiRefs.ToArray();
            }

            var newPr = GitClient.CreatePullRequestAsync(pr, TeamProjectName, RepoName).Result;

            CreateNewCommentThread(TeamProjectName, RepoName, newPr.PullRequestId, "Fix this code!!!!");

            Console.WriteLine("PR was created: " + newPr.PullRequestId);

            return newPr.PullRequestId;
        }

        /// <summary>
        /// Start new comment thread
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="RepoName"></param>
        /// <param name="PrId"></param>
        /// <param name="Title"></param>
        /// <param name="Status"></param>
        static void CreateNewCommentThread(string TeamProjectName, string RepoName, int PrId, string Title, CommentThreadStatus Status = CommentThreadStatus.Active)
        {
            GitPullRequest pr = GitClient.GetPullRequestAsync(TeamProjectName, RepoName, PrId).Result;

            GitPullRequestCommentThread gitThread = new GitPullRequestCommentThread();
            gitThread.Status = Status;
            List<Microsoft.TeamFoundation.SourceControl.WebApi.Comment> comments = new List<Microsoft.TeamFoundation.SourceControl.WebApi.Comment>();
            comments.Add(new Microsoft.TeamFoundation.SourceControl.WebApi.Comment
            { Content = Title });
            gitThread.Comments = comments;

            var thread = GitClient.CreateThreadAsync(gitThread, TeamProjectName, RepoName, PrId).Result;
        }

        /// <summary>
        /// Abandon a pull request
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="RepoName"></param>
        /// <param name="PrId"></param>
        static void AbandonPR(string TeamProjectName, string RepoName, int PrId)
        {
            CreateNewCommentThread(TeamProjectName, RepoName, PrId, "Reject this PR");

            GitPullRequest prUdated = new GitPullRequest();
            prUdated.Status = PullRequestStatus.Abandoned;

            prUdated = GitClient.UpdatePullRequestAsync(prUdated, TeamProjectName, RepoName, PrId).Result;
        }

        /// <summary>
        /// Close all active comments and complete PR
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="RepoName"></param>
        /// <param name="PrId"></param>
        static void CompletePR(string TeamProjectName, string RepoName, int PrId)
        {
            GitPullRequest pr = GitClient.GetPullRequestAsync(TeamProjectName, RepoName, PrId).Result;

            if (pr.MergeStatus != PullRequestAsyncStatus.Succeeded)
            {
                CreateNewCommentThread(TeamProjectName, RepoName, PrId, "You need to resolve conflicts");
                return;
            }

            List<GitPullRequestCommentThread> threads = GitClient.GetThreadsAsync(TeamProjectName, RepoName, PrId).Result;

            foreach (var thread in threads)
            {
                if (thread.Status == CommentThreadStatus.Active)
                {
                    GitPullRequestCommentThread updatedThread = new GitPullRequestCommentThread();
                    updatedThread.Status = CommentThreadStatus.Fixed;
                    Microsoft.TeamFoundation.SourceControl.WebApi.Comment[] comments = { new Microsoft.TeamFoundation.SourceControl.WebApi.Comment { Content = "Task is completed." } };
                    updatedThread.Comments = comments;

                    updatedThread = GitClient.UpdateThreadAsync(updatedThread, TeamProjectName, RepoName, PrId, thread.Id).Result;
                }
            }

            GitPullRequest prUdated = new GitPullRequest();
            prUdated.Status = PullRequestStatus.Completed;
            prUdated.LastMergeSourceCommit = pr.LastMergeSourceCommit;

            prUdated = GitClient.UpdatePullRequestAsync(prUdated, TeamProjectName, RepoName, PrId).Result;
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
